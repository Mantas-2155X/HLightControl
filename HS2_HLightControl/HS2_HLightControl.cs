using BepInEx;
using BepInEx.Harmony;

using HarmonyLib;

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace HS2_HLightControl
{
    [BepInPlugin(nameof(HS2_HLightControl), nameof(HS2_HLightControl), VERSION)]
    public class HS2_HLightControl : BaseUnityPlugin
    {
        public const string VERSION = "1.4.0";

        private static int multiplier = 1;
        
        private static Light backLight;
        private static Transform camLightTr;

        private static GameObject oldParent;
        private static GameObject newParent;

        private static bool lockCamLight;
        
        private void Awake() => HarmonyWrapper.PatchAll(typeof(HS2_HLightControl));
        
        private static void AddBtn(Transform background, Transform source, string name, bool resize, bool toggled, UnityAction<bool> clickEvent)
        {
            // Set names for object and text
            var copy = Instantiate(source.gameObject, source.parent);
            copy.name = name;

            var text = copy.GetComponentInChildren<Text>();
            text.text = name;
            text.resizeTextForBestFit = resize;
            
            // Clear listeners and add own custom event
            var toggle = copy.GetComponentInChildren<Toggle>();
            toggle.onValueChanged.RemoveAllListeners();
            toggle.isOn = toggled;

            toggle.onValueChanged.AddListener(clickEvent);
            
            // Lower the position
            var newRect = copy.GetComponent<RectTransform>();
            var oldLMin = newRect.offsetMin;
            var oldLMax = newRect.offsetMax;
            
            newRect.offsetMin = new Vector2(oldLMin.x, oldLMin.y - 30 * multiplier);
            newRect.offsetMax = new Vector2(oldLMax.x, oldLMax.y - 30 * multiplier);
            
            // Make background bigger
            var BackRect = background.GetComponent<RectTransform>();
            var oldSize = BackRect.sizeDelta;

            BackRect.sizeDelta = new Vector2(oldSize.x, oldSize.y + 30);

            multiplier++;
        }
        
        [HarmonyPostfix, HarmonyPatch(typeof(HSceneSprite), "SetSelectSlider")]
        public static void HSceneSprite_SetSelectSlider_CreateButtons(HSceneSprite __instance)
        {
            var backLightObj = GameObject.Find("HCamera/Main Camera/Lights Custom/Directional Light Back");
            if (backLightObj == null)
                return;
            
            var camLightObj = GameObject.Find("HCamera/Main Camera/Lights Custom/Directional Light Key");
            if (camLightObj == null)
                return;

            camLightTr = camLightObj.transform;
            
            backLight = backLightObj.GetComponent<Light>();
            if (backLight == null)
                return;

            var UI = GameObject.Find("UI");
            if (UI == null)
                return;
            
            var back = UI.transform.Find("Light/back");
            if (back == null)
                return;
            
            var orig = UI.transform.Find("Light/SubLight");
            if (orig == null)
                return;

            AddBtn(back, orig, "Backlight", false, true, delegate(bool value)
            {
                if (backLight == null)
                    return;
                
                backLight.enabled = value;
            });

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

                    __instance.ReSetLightDir(0);
                    __instance.ReSetLightDir(1);
                    
                    Destroy(newParent);
                    newParent = null;
                }
            });
        }
        
        [HarmonyPostfix, HarmonyPatch(typeof(HScene), "EndProc")]
        public static void HScene_EndProc_Cleanup() => multiplier = 1;
    }
}