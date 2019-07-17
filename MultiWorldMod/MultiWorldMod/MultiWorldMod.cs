using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GlobalEnums;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using JetBrains.Annotations;
using Modding;
using RandomizerMod;
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

        private static GameObject _shinyItem;
        private static ClientConnection connection;

        private static Dictionary<string, Dictionary<string, string>> _languageOverrides = new Dictionary<string, Dictionary<string, string>>();
        private static Dictionary<string, Sprite> _sprites;

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

        public override void Initialize(Dictionary<string, Dictionary<string, GameObject>> preloaded)
        {
            Instance = this;

            // Load images
            _sprites = ResourceHelper.GetSprites("MultiWorldMod.Resources.");

            // Create shiny object cache
            _shinyItem = Object.Instantiate(preloaded[SceneNames.Tutorial_01]["_Props/Chest/Item/Shiny Item (1)"]);
            _shinyItem.SetActive(false);
            Object.DontDestroyOnLoad(_shinyItem);

            // Parse logic xml to obtain location data
            LogicManager.ParseXML(GetType().Assembly.GetManifestResourceStream("MultiWorldMod.Resources.items.xml"));
            _itemCache = LogicManager.ItemNames.Select(name => (name, LogicManager.GetItemDef(name))).ToArray();

            ModHooks.Instance.SetPlayerBoolHook += SetBoolOverride;
            ModHooks.Instance.GetPlayerBoolHook += GetBoolOverride;
            ModHooks.Instance.NewGameHook += SetNewGameVars;
            ModHooks.Instance.LanguageGetHook += LanguageOverride;
            On.PlayMakerFSM.OnEnable += ModifyFSM;
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged += NewScene;

            MiscSceneChanges.Hook();

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
            return "0.0.3";
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

        private void AddLanguageOverride(string key, string sheetTitle, string lang)
        {
            if (!_languageOverrides.TryGetValue(sheetTitle, out Dictionary<string, string> sheet))
            {
                sheet = new Dictionary<string, string>();
                _languageOverrides[sheetTitle] = sheet;
            }

            sheet[key] = lang;
        }

        private string LanguageOverride(string key, string sheetTitle)
        {
            if (_languageOverrides.TryGetValue(sheetTitle, out Dictionary<string, string> sheet) &&
                sheet.TryGetValue(key, out string lang))
            {
                return lang;
            }

            return Language.Language.GetInternal(key, sheetTitle);
        }

        private void NewScene(Scene from, Scene to)
        {
            foreach ((string loc, ReqDef def) in _itemCache)
            {
                if (def.sceneName != to.name)
                {
                    continue;
                }

                if (def.replace)
                {
                    GameObject obj = to.FindGameObject(def.objectName);
                    if (obj == null)
                    {
                        continue;
                    }

                    ReplaceWithShiny(obj, loc);
                }
                else if (def.newShiny)
                {
                    GameObject shiny = Object.Instantiate(_shinyItem);
                    shiny.name = "Randomizer Shiny";
                    shiny.transform.position = new Vector2(def.x, def.y);
                    RemoveFling(shiny);

                    shiny.SetActive(true);

                    ModifyShiny(shiny.LocateFSM("Shiny Control"), loc);
                }
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
                        AddLanguageOverride(item.Item, "UI", itemDef.geo + " Geo");
                        itemDef.nameKey = item.Item;
                    }

                    if (item.PlayerId != connection.GetPID())
                    {
                        string newNameKey = itemDef.nameKey + "_" + item.PlayerId;
                        AddLanguageOverride(newNameKey, "UI",
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
                        _sprites[itemDef.shopSpriteKey ?? "UI.Shop.Geo"];

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

            MiscSceneChanges.SceneChanged(to);
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
                ChangeBoolTest("Binding Shield Activate", "FSM", "Check", "MultiWorldMod.Dream_Nail");
                ChangeBoolTest("Dreamer Plaque Inspect", "Conversation Control", "End", "MultiWorldMod.Dream_Nail");
                ChangeBoolTest("Dreamer Scene 2", "Control", "Init", "MultiWorldMod.Dream_Nail");
                ChangeBoolTest("PreDreamnail", "FSM", "Check", "MultiWorldMod.Dream_Nail");
                ChangeBoolTest("PostDreamnail", "FSM", "Check", "MultiWorldMod.Dream_Nail");
            }

            foreach ((string loc, ReqDef def) in _itemCache)
            {
                if (!(self.gameObject.scene.name == def.sceneName &&
                      (self.gameObject.name == def.objectName || self.gameObject.name == def.altObjectName) &&
                      self.FsmName == def.fsmName))
                {
                    continue;
                }

                switch (def.type)
                {
                    case ItemType.Big:
                    case ItemType.Charm:
                        ModifyShiny(self, loc);
                        break;
                    case ItemType.Geo:
                        ReplaceWithShiny(self.gameObject, loc);
                        break;
                    case ItemType.Spell:
                    case ItemType.Shop:
                        Log("Cannot handle location " + loc);
                        break;
                }
            }
        }

        private void ChangeBoolTest(string objectName, string fsmName, string stateName, string boolName)
        {
            GameObject obj = GameObject.Find(objectName);
            if (obj == null)
            {
                return;
            }

            PlayMakerFSM fsm = obj.LocateFSM(fsmName);
            if (fsm == null)
            {
                return;
            }

            PlayerDataBoolTest test = fsm.GetState(stateName)?.GetActionOfType<PlayerDataBoolTest>();
            if (test == null)
            {
                return;
            }

            test.boolName = boolName;
        }

        private void RemoveFling(GameObject obj)
        {
            PlayMakerFSM fsm = obj.LocateFSM("Shiny Control");

            FsmState fling = fsm.GetState("Fling?");
            fling.ClearTransitions();
            fling.AddTransition("FINISHED", "Fling R");
            FlingObject flingObj = fsm.GetState("Fling R").GetActionsOfType<FlingObject>()[0];
            flingObj.angleMin = flingObj.angleMax = 270;

            // For some reason not setting speed manually messes with the object position
            flingObj.speedMin = flingObj.speedMax = 0.1f;
        }

        private void ReplaceWithShiny(GameObject obj, string loc)
        {
            Vector3 pos = obj.transform.position;
            Transform parent = obj.transform.parent;
            Object.DestroyImmediate(obj.gameObject);

            GameObject shiny = Object.Instantiate(_shinyItem, parent, true);
            shiny.name = "Randomizer Shiny";
            shiny.transform.position = pos;
            RemoveFling(shiny);

            shiny.SetActive(loc != "Desolate_Dive");

            ModifyShiny(shiny.LocateFSM("Shiny Control"), loc);
        }

        private void ModifyShiny(PlayMakerFSM shiny, string loc)
        {
            FsmState pdBool = shiny.GetState("PD Bool?");
            FsmState charm = shiny.GetState("Charm?");
            FsmState bigGetFlash = shiny.GetState("Big Get Flash");

            // Remove actions that stop shiny from spawning
            pdBool.RemoveActionsOfType<StringCompare>();

            // Change pd bool test to our new bool
            PlayerDataBoolTest boolTest = pdBool.GetActionOfType<PlayerDataBoolTest>();
            boolTest.boolName = "MultiWorldMod." + loc;

            // Force the FSM to show the big item flash
            charm.ClearTransitions();
            charm.AddTransition("FINISHED", "Big Get Flash");

            // Tell the client about the item via SetBool
            bigGetFlash.AddAction(new SetPlayerDataBool
            {
                boolName = "MultiWorldMod." + loc,
                value = true
            });

            // Exit the fsm after giving the item
            bigGetFlash.ClearTransitions();
            bigGetFlash.AddTransition("FINISHED", "Hero Up");
            bigGetFlash.AddTransition("HERO DAMAGED", "Finish");
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
            if (LogicManager.TryGetAdditiveSet(item, out string[] additiveSet))
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
            }

            yield return ShowPopup($"Received {item} from {from}");
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
