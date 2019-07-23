using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GlobalEnums;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using JetBrains.Annotations;
using Modding;
using RandomizerLib;
using SeanprCore;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MultiWorldMod
{
    [PublicAPI]
    public class MultiWorldMod : Mod
    {
        private static readonly Sprite BlackPixel = CanvasUtil.NullSprite(new byte[] { 0x00, 0x00, 0x00, 0x55 });

        private static ClientConnection connection;

        private static Dictionary<string, string> _secondaryBools = new Dictionary<string, string>
        {
            {nameof(PlayerData.hasDash), nameof(PlayerData.canDash)},
            {nameof(PlayerData.hasShadowDash), nameof(PlayerData.canShadowDash)},
            {nameof(PlayerData.hasSuperDash), nameof(PlayerData.canSuperDash)},
            {nameof(PlayerData.hasWalljump), nameof(PlayerData.canWallJump)},
            {nameof(PlayerData.gotCharm_23), nameof(PlayerData.fragileHealth_unbreakable)},
            {nameof(PlayerData.gotCharm_24), nameof(PlayerData.fragileGreed_unbreakable)},
            {nameof(PlayerData.gotCharm_25), nameof(PlayerData.fragileStrength_unbreakable)}
        };

        private static (string, ReqDef)[] _itemCache;

        private static (string, ReqDef)[] ItemCache
        {
            get
            {
                return _itemCache ?? (_itemCache = LogicManager.ItemNames
                           .Select(name => (name, LogicManager.GetItemDef(name))).ToArray());
            }
        }

        public static MultiWorldMod Instance { get; private set; }

        public SaveSettings Settings { get; set; } = new SaveSettings();
        public GlobalSettings Config { get; set; } = new GlobalSettings();

        public override ModSettings SaveSettings
        {
            get => Settings = Settings ?? new SaveSettings();
            set => Settings = value is SaveSettings saveSettings ? saveSettings : Settings;
        }

        public override ModSettings GlobalSettings
        {
            get => Config = Config ?? new GlobalSettings();
            set => Config = value is GlobalSettings globalSettings ? globalSettings : Config;
        }

        public override void Initialize()
        {
            Instance = this;

            ModHooks.Instance.SetPlayerBoolHook += SetBoolOverride;
            ModHooks.Instance.GetPlayerBoolHook += GetBoolOverride;
            ModHooks.Instance.NewGameHook += SetNewGameVars;
            On.PlayMakerFSM.OnEnable += ModifyFSM;
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged += NewScene;

            MiscSceneChanges.Hook();
            BenchHandler.Hook();

            // Config doesn't generate values unless they are assigned
            Config.IP = Config.IP;
            Config.Port = Config.Port;
            Config.UserName = Config.UserName;

            // Setup connection to server
            connection = new ClientConnection(Config.IP, Config.Port, Config.UserName);

            connection.ItemReceived += GetItem;
            connection.MessageReceived += LogMessage;

            // Create object for UI
            GameObject obj = new GameObject();
            obj.AddComponent<MultiworldUI>();
            Object.DontDestroyOnLoad(obj);
        }

        public override string GetVersion()
        {
            return "0.0.4";
        }

        public ClientConnection.ConnectionStatus GetConnectionStatus()
        {
            return connection.GetStatus();
        }

        public void Connect(bool useOldToken)
        {
            if (useOldToken)
            {
                connection.Connect(Config.Token);
            }
            else
            {
                connection.Connect();
            }
        }

        private void NewScene(Scene from, Scene to)
        {
            foreach ((string loc, ReqDef def) in ItemCache)
            {
                if (def.sceneName != to.name)
                {
                    continue;
                }

                GameObject shiny;

                if (def.replace)
                {
                    GameObject obj = to.FindGameObject(def.objectName);
                    if (obj == null)
                    {
                        continue;
                    }

                    shiny = ShinyItemHelper.ReplaceObjectWithShiny(obj, "Randomizer Shiny");
                }
                else if (def.newShiny)
                {
                    shiny = ShinyItemHelper.CreateNewShiny(def.x, def.y, "Randomizer Shiny");
                }
                else
                {
                    continue;
                }

                ModifyShinyItem(shiny, loc);
            }

            // Shops
            foreach (string shopName in LogicManager.ShopNames)
            {
                ShopDef shopDef = LogicManager.GetShopDef(shopName);
                if (shopDef.sceneName != to.name)
                {
                    continue;
                }

                // Find the shop and save an item for use later
                GameObject shopObj = to.FindGameObject(shopDef.objectName);
                ShopMenuStock shop = shopObj.GetComponent<ShopMenuStock>();
                GameObject itemPrefab = Object.Instantiate(shop.stock[0]);
                itemPrefab.AddComponent<MultiWorldShopItem>();
                itemPrefab.SetActive(false);

                List<GameObject> newStock = new List<GameObject>();

                foreach ((string loc, PlayerItem item) in connection.GetItemsInShop(shopName))
                {
                    ReqDef itemDef = LogicManager.GetItemDef(item.Item);

                    // Create a new shop item for this item def
                    GameObject newItemObj = Object.Instantiate(itemPrefab);
                    newItemObj.SetActive(false);

                    if (itemDef.type == ItemType.Geo)
                    {
                        LanguageStringManager.SetString("UI", item.Item, itemDef.geo + " Geo");
                        itemDef.nameKey = item.Item;
                    }

                    if (item.PlayerId != connection.GetPID())
                    {
                        string newNameKey = itemDef.nameKey + "_" + item.PlayerId;
                        LanguageStringManager.SetString("UI", newNameKey,
                            Language.Language.Get(itemDef.nameKey, "UI") + " for player " + item.PlayerId);

                        itemDef.nameKey = newNameKey;
                    }

                    // Apply all the stored values
                    ShopItemStats stats = newItemObj.GetComponent<ShopItemStats>();
                    stats.playerDataBoolName = "MultiWorldMod." + loc;
                    stats.nameConvo = itemDef.nameKey;
                    stats.descConvo = itemDef.shopDescKey;
                    stats.requiredPlayerDataBool = shopDef.requiredPlayerDataBool;
                    stats.removalPlayerDataBool = string.Empty;
                    stats.dungDiscount = shopDef.dungDiscount;
                    stats.notchCostBool = itemDef.notchCost;
                    stats.cost = 250;

                    // Need to set all these to make sure the item doesn't break in one of various ways
                    stats.priceConvo = string.Empty;
                    stats.specialType = 2;
                    stats.charmsRequired = 0;
                    stats.relic = false;
                    stats.relicNumber = 0;
                    stats.relicPDInt = string.Empty;

                    // Apply the sprite for the UI
                    stats.transform.Find("Item Sprite").gameObject.GetComponent<SpriteRenderer>().sprite =
                        RandomizerLib.RandomizerLib.GetSprite(itemDef.shopSpriteKey ?? "UI.Shop.Geo");

                    newStock.Add(newItemObj);
                }

                // Save unchanged list for potential alt stock
                List<GameObject> altStock = new List<GameObject>();
                altStock.AddRange(newStock);

                // Update normal stock
                foreach (GameObject item in shop.stock)
                {
                    // It would be cleaner to destroy the unused objects, but that breaks the shop on subsequent loads
                    // TC must be reusing the shop items rather than destroying them on load
                    if (item.GetComponent<ShopItemStats>().specialType != 2 || item.GetComponent<MultiWorldShopItem>())
                    {
                        newStock.Add(item);
                    }
                }

                shop.stock = newStock.ToArray();

                // Update alt stock
                if (shop.stockAlt != null)
                {
                    foreach (GameObject item in shop.stockAlt)
                    {
                        if (item.GetComponent<ShopItemStats>().specialType != 2)
                        {
                            altStock.Add(item);
                        }
                    }

                    shop.stockAlt = altStock.ToArray();
                }
            }

            // Hard coded randomizer mode/no claw for now TODO: Not hard coded
            MiscSceneChanges.SceneChanged(to, this, true, false);
        }

        private void SetNewGameVars()
        {
            Ref.PD.hasCharm = true;

            Ref.PD.unchainedHollowKnight = true;
            Ref.PD.encounteredMimicSpider = true;
            Ref.PD.infectedKnightEncountered = true;
            Ref.PD.mageLordEncountered = true;
            Ref.PD.mageLordEncountered_2 = true;
        }

        private void ModifyFSM(On.PlayMakerFSM.orig_OnEnable orig, PlayMakerFSM self)
        {
            orig(self);

            // Dream nail fix
            if (self.gameObject.scene.name == SceneNames.RestingGrounds_04)
            {
                if (self.gameObject.name == "Binding Shield Activate" && self.FsmName == "FSM")
                {
                    ChangeBoolTest(self, "Check", "MultiWorldMod.Dream_Nail");
                }
                else if (self.gameObject.name == "Dreamer Plaque Inspect" && self.FsmName == "Conversation Control")
                {
                    ChangeBoolTest(self, "End", "MultiWorldMod.Dream_Nail");
                }
                else if (self.gameObject.name == "Dreamer Scene 2" && self.FsmName == "Control")
                {
                    ChangeBoolTest(self, "Init", "MultiWorldMod.Dream_Nail");
                }
                else if (self.gameObject.name == "PreDreamnail" && self.FsmName == "FSM")
                {
                    ChangeBoolTest(self, "Check", "MultiWorldMod.Dream_Nail");
                }
                else if (self.gameObject.name == "PostDreamnail" && self.FsmName == "FSM")
                {
                    ChangeBoolTest(self, "Check", "MultiWorldMod.Dream_Nail");
                }
            }

            foreach ((string loc, ReqDef def) in ItemCache)
            {
                if (!(self.gameObject.scene.name == def.sceneName &&
                      (self.gameObject.name == def.objectName || self.gameObject.name == def.altObjectName) &&
                      self.FsmName == def.fsmName))
                {
                    continue;
                }

                ModifyShinyItem(self.gameObject, loc);
            }
        }

        private void ModifyShinyItem(GameObject shiny, string loc)
        {
            if (!connection.GetItemAtLocation(loc, out PlayerItem item) || shiny == null || loc == null)
            {
                return;
            }

            // TODO: Handle chests in a better way than replacing them
            if (shiny.LocateFSM("Shiny Control") == null)
            {
                shiny = ShinyItemHelper.ReplaceObjectWithShiny(shiny, "Randomizer Shiny");
            }

            if (item.PlayerId != connection.GetPID())
            {
                ShinyItemHelper.ChangeIntoSimple(shiny, this, "MultiWorldMod." + loc);
            }
            else
            {
                ReqDef itemDef = LogicManager.GetItemDef(item.Item);
                switch (itemDef.type)
                {
                    case ItemType.Big:
                    case ItemType.Spell:
                        // TODO: Parse item into BigItemDef[]
                        ShinyItemHelper.ChangeIntoSimple(shiny, this, "MultiWorldMod." + loc);
                        break;
                    case ItemType.Charm:
                        ShinyItemHelper.ChangeIntoCharm(shiny, this, "MultiWorldMod." + loc, itemDef.boolName);
                        break;
                    case ItemType.Geo:
                        ShinyItemHelper.ChangeIntoGeo(shiny, this, "MultiWorldMod." + loc, itemDef.geo);
                        break;
                    case ItemType.Shop:
                    default:
                        ShinyItemHelper.ChangeIntoSimple(shiny, this, "MultiWorldMod." + loc);
                        break;
                }
            }
        }

        private void ChangeBoolTest(PlayMakerFSM fsm, string stateName, string boolName)
        {
            PlayerDataBoolTest test = fsm.GetState(stateName)?.GetActionOfType<PlayerDataBoolTest>();
            if (test == null)
            {
                return;
            }

            test.boolName = boolName;
        }

        public override List<(string, string)> GetPreloadNames()
        {
            return new List<(string, string)>
            {
                (SceneNames.Tutorial_01, "_Props/Chest/Item/Shiny Item (1)")
            };
        }

        private void LogMessage(string from, string message)
        {
            Log($"[{from}]: {message}");
        }

        private void GetItem(string from, string item)
        {
            Log($"Received item '{item}' from '{from}'");
            HeroController.instance.StartCoroutine(GiveItem(from, item));
        }

        private IEnumerator GiveItem(string from, string item)
        {
            // Handle additive items
            Dictionary<string, string[]> additiveItems =
                LogicManager.AdditiveItemNames.ToDictionary(name => name, LogicManager.GetAdditiveItems);

            string[] additiveSet = additiveItems.FirstOrDefault(pair => pair.Value.Contains(item)).Value;

            if (additiveSet != null)
            {
                foreach (ReqDef itemDef in additiveSet.Select(LogicManager.GetItemDef))
                {
                    if (Ref.PD.GetBool(itemDef.boolName))
                    {
                        continue;
                    }

                    Log("Parsed additive item to " + itemDef.boolName);

                    Ref.PD.SetBool(itemDef.boolName, true);
                    break;
                }
            }
            else
            {
                ReqDef itemDef = LogicManager.GetItemDef(item);
                if (itemDef.type == ItemType.Geo)
                {
                    Ref.Hero.AddGeo(itemDef.geo);
                }
                else
                {
                    Ref.PD.SetBool(itemDef.boolName, true);
                }
            }

            if (from != connection.GetUserName())
            {
                HeroTransitionState old = Ref.Hero.transitionState;

                while (true)
                {
                    if (old != HeroTransitionState.WAITING_TO_TRANSITION &&
                        Ref.Hero.transitionState == HeroTransitionState.WAITING_TO_TRANSITION)
                    {
                        break;
                    }

                    old = Ref.Hero.transitionState;
                    yield return new WaitForEndOfFrame();
                }

                yield return ShowPopup($"Received {item} from {from}");
            }
        }

        private IEnumerator ShowPopup(string text)
        {
            // Create overlay
            GameObject canvas = CanvasUtil.CreateCanvas(RenderMode.ScreenSpaceOverlay, new Vector2(1920, 1080));
            CanvasUtil.CreateImagePanel(canvas, BlackPixel,
                    new CanvasUtil.RectData(Vector2.zero, Vector2.zero, Vector2.zero, Vector2.one))
                .GetComponent<Image>()
                .preserveAspect = false;

            // Create text on overlay
            CanvasUtil.CreateTextPanel(canvas, text, 34,
                TextAnchor.MiddleCenter,
                new CanvasUtil.RectData(new Vector2(1920, 100), Vector2.zero, new Vector2(0.5f, 0.55f),
                    new Vector2(0.5f, 0.55f)), Fonts.Get("Perpetua"));

            float time = 0;
            while (time < 2f)
            {
                yield return new WaitForEndOfFrame();
                time += Time.deltaTime;
            }

            Object.DestroyImmediate(canvas);
        }

        // Bool hooks for special cases
        private bool GetBoolOverride(string boolName)
        {
            switch (boolName)
            {
                // Fake spell bools
                case "hasVengefulSpirit":
                    return Ref.PD.fireballLevel > 0;
                case "hasShadeSoul":
                    return Ref.PD.fireballLevel > 1;
                case "hasDesolateDive":
                    return Ref.PD.quakeLevel > 0;
                case "hasDescendingDark":
                    return Ref.PD.quakeLevel > 1;
                case "hasHowlingWraiths":
                    return Ref.PD.screamLevel > 0;
                case "hasAbyssShriek":
                    return Ref.PD.screamLevel > 1;
            }

            if (boolName == nameof(PlayerData.gotSlyCharm))
            {
                return Ref.PD.GetBool("MultiWorldMod.Nailmaster's_Glory");
            }

            if (boolName.StartsWith("MultiWorldMod."))
            {
                return Settings.GetBool(false, boolName.Substring(14));
            }

            return Ref.PD.GetBoolInternal(boolName);
        }

        private void SetBoolOverride(string boolName, bool value)
        {
            switch (boolName)
            {
                case "hasVengefulSpirit" when value && Ref.PD.fireballLevel <= 0:
                    Ref.PD.SetInt("fireballLevel", 1);
                    break;
                case "hasShadeSoul" when value:
                    Ref.PD.SetInt("fireballLevel", 2);
                    break;
                case "hasDesolateDive" when value && Ref.PD.quakeLevel <= 0:
                    Ref.PD.SetInt("quakeLevel", 1);
                    break;
                case "hasDescendingDark" when value:
                    Ref.PD.SetInt("quakeLevel", 2);
                    break;
                case "hasHowlingWraiths" when value && Ref.PD.screamLevel <= 0:
                    Ref.PD.SetInt("screamLevel", 1);
                    break;
                case "hasAbyssShriek" when value:
                    Ref.PD.SetInt("screamLevel", 2);
                    break;
            }

            if (boolName.StartsWith("MultiWorldMod."))
            {
                boolName = boolName.Substring(14);
                if (connection.GetItemAtLocation(boolName, out PlayerItem item))
                {
                    if (item.PlayerId != connection.GetPID())
                    {
                        HeroController.instance.StartCoroutine(
                            ShowPopup($"Obtained {item.Item} for player {item.PlayerId}"));
                    }

                    connection.ObtainItem(boolName);
                }

                Settings.SetBool(value, boolName);
                return;
            }

            Ref.PD.SetBoolInternal(boolName, value);

            // Check if there is a secondary bool for this item
            if (_secondaryBools.TryGetValue(boolName, out string secondaryBoolName))
            {
                Ref.PD.SetBool(secondaryBoolName, value);
            }

            if (boolName == nameof(PlayerData.hasCyclone) || boolName == nameof(PlayerData.hasUpwardSlash) ||
                boolName == nameof(PlayerData.hasDashSlash))
            {
                // Make nail arts work
                bool hasCyclone = Ref.PD.GetBool(nameof(PlayerData.hasCyclone));
                bool hasUpwardSlash = Ref.PD.GetBool(nameof(PlayerData.hasUpwardSlash));
                bool hasDashSlash = Ref.PD.GetBool(nameof(PlayerData.hasDashSlash));

                Ref.PD.SetBool(nameof(PlayerData.hasNailArt), hasCyclone || hasUpwardSlash || hasDashSlash);
                Ref.PD.SetBool(nameof(PlayerData.hasAllNailArts), hasCyclone && hasUpwardSlash && hasDashSlash);
            }
            else if (boolName == nameof(PlayerData.hasDreamGate) && value)
            {
                // Make sure the player can actually use dream gate after getting it
                FSMUtility.LocateFSM(Ref.Hero.gameObject, "Dream Nail").FsmVariables
                    .GetFsmBool("Dream Warp Allowed").Value = true;
            }
            else if (boolName == nameof(PlayerData.hasAcidArmour) && value)
            {
                // Gotta update the acid pools after getting this
                PlayMakerFSM.BroadcastEvent("GET ACID ARMOUR");
            }
        }
    }
}
