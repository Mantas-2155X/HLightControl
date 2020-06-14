using BepInEx;
using BepInEx.Harmony;

using HarmonyLib;

using Illusion.Game;
using TMPro;

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace KK_HLightControl
{
    [BepInPlugin(nameof(KK_HLightControl), nameof(KK_HLightControl), VERSION)]
    public class KK_HLightControl : BaseUnityPlugin
    {
        public const string VERSION = "1.0.0";
        
        private static Transform camLightTr;

        private static GameObject oldParent;
        private static GameObject newParent;

        private static bool lockCamLight;
        private static bool created;
        
        private void Awake() => HarmonyWrapper.PatchAll(typeof(KK_HLightControl));
        
        private static void AddBtn(Transform background, Transform source, string name, bool resize, bool toggled, UnityAction<bool> clickEvent)
        {
            // Set names for object and text
            var copy = Instantiate(source.gameObject, background);
            copy.name = name;

            var text = copy.GetComponentInChildren<TextMeshProUGUI>();
            text.text = name;
            text.autoSizeTextContainer = resize;
            
            // Clear listeners and add own custom event
            var toggle = copy.GetComponentInChildren<Toggle>();
            toggle.onValueChanged.RemoveAllListeners();
            toggle.isOn = toggled;

            toggle.onValueChanged.AddListener(clickEvent);

            // Align position
            var newRect = copy.GetComponent<RectTransform>();
            
            var oldLMin = newRect.offsetMin;
            var oldLMax = newRect.offsetMax;
            
            newRect.offsetMin = new Vector2(10, oldLMin.y);
            newRect.offsetMax = new Vector2(278, oldLMax.y);
            newRect.sizeDelta = new Vector2(268, 36);

            for(var i = 0; i < copy.transform.childCount; i++)
            {
                var child = copy.transform.GetChild(i);
                var oldpos = child.localPosition;
                
                child.localPosition = new Vector3(oldpos.x + 10, oldpos.y - 4, oldpos.z);
            }
        }
        
        [HarmonyPostfix, HarmonyPatch(typeof(HSprite), "OnMainMenu")]
        public static void HSprite_OnMainMenu_CreateButtons(HSprite __instance)
        {
            if (created)
                return;

            var camLightObj = GameObject.Find("HScene/CameraBase/Camera/Directional Light");
            if (camLightObj == null)
                return;
            
            camLightTr = camLightObj.transform;

            var Canvas = GameObject.Find("Canvas");
            if (Canvas == null)
                return;
            
            var back = Canvas.transform.Find("SubMenu/LightGroup/light");
            if (back == null)
                return;
            
            var orig = Canvas.transform.Find("SubMenu/MoveGroup/move/Toggle");
            if (orig == null)
                return;

            AddBtn(back, orig, "Lock Camlight", true, false, delegate(bool value)
            {
                lockCamLight = value;

                if (lockCamLight)
                {
                    oldParent = camLightTr.parent.gameObject;

                    newParent = new GameObject("CamLightLock");
                    newParent.transform.position = oldParent.transform.position;
                    newParent.transform.eulerAngles = oldParent.transform.eulerAngles;

                    camLightTr.parent = newParent.transform;
                }
                else if(oldParent != null)
                {
                    camLightTr.parent = oldParent.transform;

                    __instance.OnClickLightDirInit(0);
                    __instance.OnClickLightDirInit(1);
               
                    Destroy(newParent);
                    newParent = null;
                }
                
                Utils.Sound.Play(SystemSE.sel);
            });

            created = true;
        }
        
        [HarmonyPostfix, HarmonyPatch(typeof(HSceneProc), "EndProc")]
        public static void HSceneProc_EndProc_Cleanup() => created = false;
    }
}