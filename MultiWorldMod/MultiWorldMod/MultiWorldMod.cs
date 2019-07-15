using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using JetBrains.Annotations;
using Modding;
using SeanprCore;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MultiWorldMod
{
    [PublicAPI]
    public class MultiWorldMod : Mod
    {
        private static GameObject _shinyItem;
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

        public static MultiWorldMod Instance { get; private set; }

        public SaveSettings Settings { get; set; } = new SaveSettings();

        public override ModSettings SaveSettings
        {
            get => Settings = Settings ?? new SaveSettings();
            set => Settings = value is SaveSettings saveSettings ? saveSettings : Settings;
        }

        public override void Initialize(Dictionary<string, Dictionary<string, GameObject>> preloaded)
        {
            Instance = this;

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
            On.PlayMakerFSM.OnEnable += ModifyFSM;
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged += NewScene;

            // Setup connection to server
            connection = new ClientConnection("127.0.0.1", 38281, "Sean");

            connection.ItemReceived += GetItem;
            connection.MessageReceived += LogMessage;
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
                    shiny.transform.position = new Vector2(def.x, def.y);
                    RemoveFling(shiny);

                    shiny.SetActive(true);

                    ModifyShiny(shiny.LocateFSM("Shiny Control"), loc);
                }
            }
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
            Object.DestroyImmediate(obj.gameObject);

            GameObject shiny = Object.Instantiate(_shinyItem);
            shiny.transform.position = pos;
            RemoveFling(shiny);

            shiny.SetActive(true);

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
            HeroController.instance.StartCoroutine(GiveItem(item));
        }

        private IEnumerator GiveItem(string item)
        {
            float time = 0;
            while (time < 0f)
            {
                Time.timeScale = 0;

                yield return new WaitForEndOfFrame();
                time += Time.unscaledDeltaTime;
            }

            Time.timeScale = 1f;

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
                return Settings.SlyCharm;
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
                if (LogicManager.ItemNames.Contains(boolName))
                {
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
