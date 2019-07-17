using SeanprCore;
using UnityEngine;

namespace MultiWorldMod
{
    public class MultiworldUI : MonoBehaviour
    {
        private void OnGUI()
        {
            if (Ref.GM.GetSceneNameString() != Constants.MENU_SCENE)
            {
                return;
            }

            if (GUI.Button(new Rect(Screen.width - 200, 0, 200, 100), "Connect"))
            {
                MultiWorldMod.Instance.Connect(false);
            }
            else if (GUI.Button(new Rect(Screen.width - 200, 150, 200, 100), "Reconnect (old token)"))
            {
                MultiWorldMod.Instance.Connect(true);
            }
        }
    }
}
