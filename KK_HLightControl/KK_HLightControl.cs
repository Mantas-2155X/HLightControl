using System.Linq;

using BepInEx;
using BepInEx.Configuration;

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
        public const string VERSION = "1.1.1";
        
        private static Transform camLightTr;

        private static GameObject oldParent;
        private static GameObject newParent;

        private static bool lockCamLight;
        private static bool created;
        
        private static Light[] lights;
        private static int[] resolutions;
        
        private static ConfigEntry<int> customShadowResolution { get; set; }
        
        private void Awake()
        {
            customShadowResolution = Config.Bind("General", "Shadow resolution target", 1024, new ConfigDescription("What resolution to apply when clicking 'Lower shadow resolution'"));

            Harmony.CreateAndPatchAll(typeof(KK_HLightControl));
        }

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
            
            for (var i = 0; i < toggle.onValueChanged.GetPersistentEventCount(); i++)
                toggle.onValueChanged.SetPersistentListenerState(i, UnityEventCallState.Off);
            
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

            lights = FindObjectsOfType<Light>();
            resolutions = new int[lights.Length];
            
            for (var i = 0; i < lights.Length; i++)
                resolutions[i] = lights[i] != null ? lights[i].shadowCustomResolution : -1;
            
            AddBtn(back, orig, "Lock Camlight", false, false, delegate(bool value) { btn_LockCamLight(value, __instance); });
            AddBtn(back, orig, "Lower Shadow Resolution", true, false, btn_LowerLightsResolution);

            created = true;
        }
        
        [HarmonyPostfix, HarmonyPatch(typeof(HSceneProc), "EndProc")]
        public static void HSceneProc_EndProc_Cleanup()
        {
            created = false;
            btn_LowerLightsResolution(false);
        }

        private static void btn_LockCamLight(bool value, HSprite __instance)
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
        }
        
        private static void btn_LowerLightsResolution(bool value)
        {
            if (value)
            {
                foreach (var t in lights.Where(t => t != null))
                    t.shadowCustomResolution = customShadowResolution.Value;
            }
            else
            {
                for (var i = 0; i < lights.Length; i++)
                    if(lights[i] != null)
                        lights[i].shadowCustomResolution = resolutions[i];
            }
        }
    }
}