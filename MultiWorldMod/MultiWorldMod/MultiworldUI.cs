using SeanprCore;
using UnityEngine;

namespace MultiWorldMod
{
    public class MultiworldUI : MonoBehaviour
    {
        private bool _clickedConnect;

        private void OnGUI()
        {
            if (Ref.GM.GetSceneNameString() != Constants.MENU_SCENE)
            {
                return;
            }

            if (!_clickedConnect && GUI.Button(new Rect(Screen.width - 200, 0, 200, 100), "Connect"))
            {
                MultiWorldMod.Instance.Connect(false);
                _clickedConnect = true;
            }
            else if (GUI.Button(new Rect(Screen.width - 200, 150, 200, 100), "Reconnect (old token)"))
            {
                MultiWorldMod.Instance.Connect(true);
                _clickedConnect = true;
            }

            GUI.Label(new Rect(Screen.width - 200, 300, 200, 100),
                MultiWorldMod.Instance.GetConnectionStatus().ToString());
        }
    }
}
