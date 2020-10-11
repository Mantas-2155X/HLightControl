using System.Collections.Generic;
using System.Linq;

using BepInEx;
using BepInEx.Configuration;

using HarmonyLib;

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using Resources = UnityEngine.Resources;

namespace HS2_HLightControl
{
    [BepInProcess("HoneySelect2")]
    [BepInPlugin(nameof(HS2_HLightControl), nameof(HS2_HLightControl), VERSION)]
    public class HS2_HLightControl : BaseUnityPlugin
    {
        public const string VERSION = "1.2.3";

        private static int multiplier = 1;
        
        private static HSceneSprite sprite;
        
        private static Light backLight;
        private static Transform camLightTr;

        private static GameObject oldParent;
        private static GameObject newParent;

        private static bool lockCamLight;
        private static bool created;

        private static Light[] lights;
        private static int[] resolutions;
        
        private static List<Toggle> toggles;
        private static readonly List<NewToggleInfo> toggleInfo = new List<NewToggleInfo>()
        {
            new NewToggleInfo("Backlight", false, true, btn_BackLight),
            new NewToggleInfo("Lock Camlight", false, false, btn_LockCamLight),
            new NewToggleInfo("Lower Shadow Resolution", true, false, btn_LowerLightsResolution)
        };
        private static Toggle auxiliaryLightToggle;
        
        private static readonly List<ConfigEntry<bool>> btn = new List<ConfigEntry<bool>>();

        private static ConfigEntry<bool> auxiliaryLight { get; set; }
        private static ConfigEntry<int> customShadowResolution { get; set; }

        private void Awake()
        {
            customShadowResolution = Config.Bind("General", "Shadow resolution target", 1024, new ConfigDescription("What resolution to apply when clicking 'Lower shadow resolution'"));
            auxiliaryLight = Config.Bind("Defaults", "Auxiliary Light", true);

            btn.Clear();
            
            foreach (var t in toggleInfo)
                btn.Add(Config.Bind("Defaults", t.name, t.toggled));

            for (var i = 0; i < toggleInfo.Count; i++)
            {
                var index = i;
                
                btn[i].SettingChanged += delegate
                {
                    if (!created || toggles == null || toggles.Count <= index || toggles[index] == null)
                        return;
                    
                    toggles[index].isOn = btn[index].Value;
                };
            }
            
            var harmony = new Harmony(nameof(HS2_HLightControl));
            harmony.PatchAll(typeof(HS2_HLightControl));
        }

        private static void AddBtn(Transform background, Transform source, string name, bool resize, UnityAction<bool> clickEvent)
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

            foreach (var b in btn.Where(b => name == b.Definition.Key))
                toggle.isOn = !b.Value;

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

            toggles.Add(toggle);

            foreach (var b in btn.Where(b => name == b.Definition.Key))
                toggle.isOn = b.Value;
            
            multiplier++;
        }
        
        [HarmonyPostfix, HarmonyPatch(typeof(HSceneSprite), "SetLightInfo")]
        public static void HSceneSprite_SetLightInfo_CreateButtons(HSceneSprite __instance)
        {
            sprite = __instance;
            
            if (created)
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

            toggles = new List<Toggle>();

            lights = Resources.FindObjectsOfTypeAll<Light>();
            resolutions = new int[lights.Length];
            
            for (var i = 0; i < lights.Length; i++)
                resolutions[i] = lights[i] != null ? lights[i].shadowCustomResolution : -1;

            foreach (var light in lights)
            {
                var parent = light.transform;

                switch (parent.name)
                {
                    case "Directional Light Key":
                        camLightTr = parent;
                        break;
                    case "Directional Light Back":
                        backLight = light;
                        break;
                }
            }
            
            var text = orig.GetComponentInChildren<Text>();
            text.alignment = TextAnchor.MiddleLeft;

            // Make textbox wider and move to the right
            var textRect = text.gameObject.GetComponent<RectTransform>();
            var oldTeMin = textRect.offsetMin;
            var oldTeMax = textRect.offsetMax;
            
            textRect.offsetMin = new Vector2(128, oldTeMin.y);
            textRect.offsetMax = new Vector2(348, oldTeMax.y);
            textRect.sizeDelta = new Vector2(220, 30);
            
            var toggleComp = orig.GetComponentInChildren<Toggle>();
            
            // Move toggle to the left
            var toggleRect = toggleComp.gameObject.GetComponent<RectTransform>();
            var oldToMin = toggleRect.offsetMin;
            var oldToMax = toggleRect.offsetMax;
            
            toggleRect.offsetMin = new Vector2(98, oldToMin.y);
            toggleRect.offsetMax = new Vector2(128, oldToMax.y);
            toggleRect.sizeDelta = new Vector2(30, 30);
            
            foreach (var toggle in toggleInfo)
                AddBtn(back, orig, toggle.name, toggle.resize, toggle.clickEvent);

            var sub = UI.transform.Find("Light/SubLight");
            
            if (sub != null)
                auxiliaryLightToggle = sub.GetComponentInChildren<Toggle>();

            if (auxiliaryLightToggle != null)
                auxiliaryLightToggle.isOn = auxiliaryLight.Value;
            
            created = true;
        }
        
        [HarmonyPostfix, HarmonyPatch(typeof(HScene), "EndProc")]
        public static void HScene_EndProc_Cleanup()
        {
            multiplier = 1;
            created = false;

            if (toggles != null)
            {
                for (var i = 0; i < toggles.Count; i++)
                    toggles[i].isOn = toggleInfo[i].toggled;

                toggles.Clear();
                toggles = null;
            }
            
            if (auxiliaryLightToggle != null)
                auxiliaryLightToggle.isOn = true;
        }
        
        [HarmonyPostfix, HarmonyPatch(typeof(HColorPickerCtrl), "Open")]
        public static void HColorPickerCtrl_Open_AdjustColorPnl(HColorPickerCtrl __instance)
        {
            var colorPnl = __instance.transform;
            var cRect = colorPnl.GetComponent<RectTransform>();

            var oCMin = cRect.offsetMin;
            var oCMax = cRect.offsetMax;
            
            cRect.offsetMin = new Vector2(oCMin.x, oCMin.y - 30 * (multiplier - 1));
            cRect.offsetMax = new Vector2(oCMax.x, oCMax.y - 30 * (multiplier - 1));
        }

        private static void btn_BackLight(bool value)
        {
            if (backLight == null)
                return;
                
            backLight.enabled = value;
        }
        
        private static void btn_LockCamLight(bool value)
        {
            lockCamLight = value;

            if (camLightTr == null)
                return;

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

                sprite.ReSetLightDir(0);
                sprite.ReSetLightDir(1);
                    
                Destroy(newParent);
                newParent = null;
            }
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
    
    public class NewToggleInfo
    {
        public readonly string name;
        
        public readonly bool resize;
        public readonly bool toggled;
        
        public readonly UnityAction<bool> clickEvent;

        public NewToggleInfo(string _name, bool _resize, bool _toggled, UnityAction<bool> _clickEvent)
        {
            name = _name;
            resize = _resize;
            toggled = _toggled;
            clickEvent = _clickEvent;
        }
    }
}