﻿using System.Collections.Generic;
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
    [BepInProcess("Koikatu")]
    [BepInProcess("Koikatsu Party")]
    [BepInPlugin(nameof(KK_HLightControl), nameof(KK_HLightControl), VERSION)]
    public class KK_HLightControl : BaseUnityPlugin
    {
        public const string VERSION = "1.2.4";
        
        private static HSprite sprite;
        
        private static Transform camLightTr;

        private static GameObject oldParent;
        private static GameObject newParent;

        private static bool lockCamLight;
        private static bool created;
        private static bool mute;

        private static Light[] lights;
        private static int[] resolutions;
        
        private static List<Toggle> toggles;
        private static readonly List<NewToggleInfo> toggleInfo = new List<NewToggleInfo>()
        {
            new NewToggleInfo("Lock Camlight", false, false, btn_LockCamLight),
            new NewToggleInfo("Lower Shadow Resolution", true, false, btn_LowerLightsResolution)
        };
        
        private static readonly List<ConfigEntry<bool>> btn = new List<ConfigEntry<bool>>();

        private static ConfigEntry<int> customShadowResolution { get; set; }
        
        private void Awake()
        {
            customShadowResolution = Config.Bind("General", "Shadow resolution target", 1024, new ConfigDescription("What resolution to apply when clicking 'Lower shadow resolution'"));
            
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
            
            var harmony = new Harmony(nameof(KK_HLightControl));
            harmony.PatchAll(typeof(KK_HLightControl));
        }

        private static void AddBtn(Transform background, Transform source, string name, bool resize, UnityAction<bool> clickEvent)
        {
            // Set names for object and text
            var copy = Instantiate(source.gameObject, background);
            copy.name = name;
            
            var text = copy.GetComponentInChildren<TextMeshProUGUI>();
            text.text = name;

            // peepodumb
            // Need to call this to populate the private RT field or autoSizeTextContainer will crash
            var _ = text.rectTransform;
            text.autoSizeTextContainer = resize;
            
            // Clear listeners and add own custom event
            var toggle = copy.GetComponentInChildren<Toggle>();
            
            for (var i = 0; i < toggle.onValueChanged.GetPersistentEventCount(); i++)
                toggle.onValueChanged.SetPersistentListenerState(i, UnityEventCallState.Off);
            
            toggle.onValueChanged.RemoveAllListeners();
            
            foreach (var b in btn.Where(b => name == b.Definition.Key))
                toggle.isOn = !b.Value;
            
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
            
            toggles.Add(toggle);
            
            foreach (var b in btn.Where(b => name == b.Definition.Key))
                toggle.isOn = b.Value;
        }
        
        [HarmonyPostfix, HarmonyPatch(typeof(HSprite), "SetLightData")]
        public static void HSprite_SetLightData_CreateButtons(HSprite __instance)
        {
            sprite = __instance;
            
            if (created)
                return;
            
            var Canvas = __instance.transform;
            if (Canvas == null)
                return;
            
            var back = Canvas.Find("SubMenu/LightGroup/light");
            if (back == null)
                return;
            
            var orig = Canvas.Find("SubMenu/MoveGroup/move/Toggle");
            if (orig == null)
                return;
            
            toggles = new List<Toggle>();
            
            lights = FindObjectsOfType<Light>();
            resolutions = new int[lights.Length];
            
            for (var i = 0; i < lights.Length; i++)
                resolutions[i] = lights[i] != null ? lights[i].shadowCustomResolution : -1;
            
            foreach (var light in lights)
            {
                var parent = light.transform;

                switch (parent.name)
                {
                    case "Directional Light":
                        camLightTr = parent;
                        break;
                }
            }

            mute = true;
            foreach (var toggle in toggleInfo)
                AddBtn(back, orig, toggle.name, toggle.resize, toggle.clickEvent);
            mute = false;

            created = true;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(HSceneProc), "EndProc")]
        public static void HSceneProc_EndProc_Cleanup()
        {
            created = false;
            mute = true;

            if (toggles != null)
            {
                for (var i = 0; i < toggles.Count; i++)
                    toggles[i].isOn = toggleInfo[i].toggled;

                toggles = null;
            }
            mute = false;
        }

        private static void btn_LockCamLight(bool value)
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

                sprite.OnClickLightDirInit(0);
                sprite.OnClickLightDirInit(1);
               
                Destroy(newParent);
                newParent = null;
            }
                
            if(!mute)
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
                
            if(!mute)
                Utils.Sound.Play(SystemSE.sel);
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