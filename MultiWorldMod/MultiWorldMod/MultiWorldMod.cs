using System;
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
        }

        public override List<(string, string)> GetPreloadNames()
        {
            return new List<(string, string)>
            {
                (SceneNames.Tutorial_01, "_Props/Chest/Item/Shiny Item (1)")
            };
        }
    }
}
