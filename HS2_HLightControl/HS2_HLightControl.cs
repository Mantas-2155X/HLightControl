﻿using System.Linq;

using BepInEx;
using BepInEx.Configuration;

using HarmonyLib;

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace HS2_HLightControl
{
    [BepInPlugin(nameof(HS2_HLightControl), nameof(HS2_HLightControl), VERSION)]
    public class HS2_HLightControl : BaseUnityPlugin
    {
        public const string VERSION = "1.1.1";

        private static int multiplier = 1;
        
        private static Light backLight;
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

            Harmony.CreateAndPatchAll(typeof(HS2_HLightControl));
        }

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
            if (created)
                return;
            
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

            var text = orig.GetComponentInChildren<Text>();
            text.alignment = TextAnchor.MiddleLeft;

            // Make textbox wider and move to the right
            var textRect = text.gameObject.GetComponent<RectTransform>();
            var oldTeMin = textRect.offsetMin;
            var oldTeMax = textRect.offsetMax;
            
            textRect.offsetMin = new Vector2(128, oldTeMin.y);
            textRect.offsetMax = new Vector2(348, oldTeMax.y);
            textRect.sizeDelta = new Vector2(220, 30);
            
            var toggle = orig.GetComponentInChildren<Toggle>();
            
            // Move toggle to the left
            var toggleRect = toggle.gameObject.GetComponent<RectTransform>();
            var oldToMin = toggleRect.offsetMin;
            var oldToMax = toggleRect.offsetMax;
            
            toggleRect.offsetMin = new Vector2(98, oldToMin.y);
            toggleRect.offsetMax = new Vector2(128, oldToMax.y);
            toggleRect.sizeDelta = new Vector2(30, 30);
            
            lights = FindObjectsOfType<Light>();
            resolutions = new int[lights.Length];
            
            for (var i = 0; i < lights.Length; i++)
                resolutions[i] = lights[i] != null ? lights[i].shadowCustomResolution : -1;

            AddBtn(back, orig, "Backlight", false, true, btn_BackLight);
            AddBtn(back, orig, "Lock Camlight", false, false, delegate(bool value) { btn_LockCamLight(value, __instance); });
            AddBtn(back, orig, "Lower Shadow Resolution", true, false, btn_LowerLightsResolution);

            created = true;
        }
        
        [HarmonyPostfix, HarmonyPatch(typeof(HScene), "EndProc")]
        public static void HScene_EndProc_Cleanup()
        {
            multiplier = 1;
            created = false;
            btn_LowerLightsResolution(false);
        }

        private static void btn_BackLight(bool value)
        {
            if (backLight == null)
                return;
                
            backLight.enabled = value;
        }
        
        private static void btn_LockCamLight(bool value, HSceneSprite __instance)
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