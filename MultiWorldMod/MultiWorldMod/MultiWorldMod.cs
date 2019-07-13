using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HutongGames.PlayMaker.Actions;
using Modding;
using SeanprCore;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MultiWorldMod
{
    public class MultiWorldMod : Mod
    {
        private static GameObject _flashEffect;
        private static ClientConnection connection;

        public static MultiWorldMod Instance { get; private set; }

        public override void Initialize(Dictionary<string, Dictionary<string, GameObject>> preloaded)
        {
            Instance = this;

            _flashEffect = Object.Instantiate(preloaded[SceneNames.Tutorial_01]["_Props/Chest/Item/Shiny Item (1)"]
                .LocateFSM("Shiny Control").GetState("Big Get Flash").GetActionOfType<SpawnObjectFromGlobalPool>()
                .gameObject.Value);

            _flashEffect.SetActive(false);
            Object.DontDestroyOnLoad(_flashEffect);

            connection = new ClientConnection("127.0.0.1", 5001, "Sean");
            connection.ItemReceived += (from, item) =>
            {
                Log($"Received item '{item}' from '{from}'");
                HeroController.instance.StartCoroutine(GiveItem(item));
            };

            connection.MessageReceived += (from, message) =>
            {
                Log($"[{from}]: {message}");
            };
        }

        public override List<(string, string)> GetPreloadNames()
        {
            return new List<(string, string)>
            {
                (SceneNames.Tutorial_01, "_Props/Chest/Item/Shiny Item (1)")
            };
        }

        private IEnumerator GiveItem(string item)
        {
            try
            {
                FSMUtility.SendEventToGameObject(HeroController.instance.gameObject, "FSM CANCEL", true);
                HeroController.instance.RelinquishControl();
                HeroController.instance.AffectedByGravity(true);
                HeroController.instance.StopAnimationControl();

                HeroController.instance.GetComponent<Rigidbody2D>().velocity = Vector2.zero;
            }
            catch (Exception e)
            {
                LogError(e);
            }

            float time = 0;
            while (time < 1.5f)
            {
                Time.timeScale = 0;

                yield return new WaitForEndOfFrame();
                time += Time.unscaledDeltaTime;
            }

            try
            {
                Time.timeScale = 1f;

                HeroController.instance.RegainControl();
                HeroController.instance.StartAnimationControl();

                switch (item)
                {
                    case "MOTHWING_CLOAK":
                        PlayerData.instance.hasDash = true;
                        PlayerData.instance.canDash = true;
                        break;
                    case "MANTIS_CLAW":
                        PlayerData.instance.hasWalljump = true;
                        PlayerData.instance.canWallJump = true;
                        break;
                }
            }
            catch (Exception e)
            {
                LogError(e);
            }
        }
    }
}
