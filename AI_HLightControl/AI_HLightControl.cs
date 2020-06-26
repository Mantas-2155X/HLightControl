using System.Collections.Generic;
using System.Linq;

using BepInEx;
using BepInEx.Configuration;

using HarmonyLib;

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

using UnityEx;

namespace AI_HLightControl
{
    [BepInProcess("AI-Syoujyo")]
    [BepInPlugin(nameof(AI_HLightControl), nameof(AI_HLightControl), VERSION)]
    public class AI_HLightControl : BaseUnityPlugin
    {
        public const string VERSION = "1.1.2";

        private static int multiplier = 1;

        private static HSceneSprite sprite;
        private static GameObject Content;
        
        private static List<Light> backLights;
        private static List<Light> auxLights;
        private static Transform[] camLightTrs;

        private static GameObject[] oldParents;
        private static GameObject[] newParents;

        private static bool lockCamLight;
        private static bool created;

        private static Light[] lights;
        private static int[] resolutions;

        private static List<Toggle> toggles;
        private static readonly List<NewToggleInfo> toggleInfo = new List<NewToggleInfo>()
        {
            new NewToggleInfo("Backlight", false, true, btn_BackLight),
            new NewToggleInfo("Auxiliary", false, true, btn_Auxiliary),
            new NewToggleInfo("Lock Camlight", false, false, btn_LockCamLight),
            new NewToggleInfo("Lower Shadow Resolution", true, false, btn_LowerLightsResolution)
        };
        
        private static ConfigEntry<int> customShadowResolution { get; set; }

        private void Awake()
        {
            customShadowResolution = Config.Bind("General", "Shadow resolution target", 1024, new ConfigDescription("What resolution to apply when clicking 'Lower shadow resolution'"));

            Harmony.CreateAndPatchAll(typeof(AI_HLightControl));
        }

        private static void AddBtn(Transform content, Transform source, string name, bool resize, bool toggled, UnityAction<bool> clickEvent)
        {
            // Set names for object and text
            var copy = Instantiate(source.gameObject, content);
            copy.name = name;

            var text = copy.GetComponentInChildren<Text>();
            text.text = name;
            text.resizeTextForBestFit = resize;

            // Glow on mouse hover
            var bg = copy.transform.Find("Background");
            var glow = bg.GetComponent<Image>();

            var enter = copy.GetComponent<PointerEnterTrigger>();
            var exit = copy.GetComponent<PointerExitTrigger>();
            
            enter.Triggers.Clear();
            exit.Triggers.Clear();
            
            var enterTrigger = new UITrigger.TriggerEvent();
            var exitTrigger = new UITrigger.TriggerEvent();
            
            enterTrigger.AddListener(data => { glow.enabled = true; });
            exitTrigger.AddListener(data => { glow.enabled = false; });
            
            enter.Triggers.Add(enterTrigger);
            exit.Triggers.Add(exitTrigger);
            
            // Make the image smaller
            var image = copy.transform.Find("Background/Checkmark");
            var imageComp = image.GetComponent<Image>();
            var imageRect = image.GetComponent<RectTransform>();
            imageRect.offsetMin = new Vector2(-80, -20);
            imageRect.offsetMax = new Vector2(80, 20);
            imageRect.sizeDelta = new Vector2(160, 40);
            
            // Clear listeners and add own custom event
            var toggle = copy.GetComponentInChildren<Toggle>();
            toggle.onValueChanged.RemoveAllListeners();
            
            for (var i = 0; i < toggle.onValueChanged.GetPersistentEventCount(); i++)
                toggle.onValueChanged.SetPersistentListenerState(i, UnityEventCallState.Off);

            toggle.group = null;
            
            toggle.onValueChanged.AddListener(clickEvent);
            toggle.onValueChanged.AddListener((state) => { imageComp.enabled = state; });
            
            toggle.isOn = toggled;

            // Lower the position
            var newRect = copy.GetComponent<RectTransform>();

            newRect.offsetMin = new Vector2(0, -90 - 50 * multiplier);
            newRect.offsetMax = new Vector2(170, -40 - 50 * multiplier);
            newRect.sizeDelta = new Vector2(170, 50);

            toggles.Add(toggle);
            
            multiplier++;
        }
        
        [HarmonyPostfix, HarmonyPatch(typeof(HSceneSprite), "SetLightInfo")]
        public static void HSceneSprite_SetLightInfo_CreateButtons(HSceneSprite __instance)
        {
            sprite = __instance;

            created = Content != null;
            
            if (created)
                return;
            
            oldParents = new GameObject[2];
            newParents = new GameObject[2];

            toggles?.Clear();

            toggles = new List<Toggle>();
            
            var UI = GameObject.Find("CommonSpace/HSceneUISet");
            
            var Btn = UI.transform.Find("Canvas/CanvasGroup/Panel/CoordinatesCard/SortPanel/SortCategory/Name");
            var LightAdj = UI.transform.Find("Canvas/CanvasGroup/LightCategory/BG/LightAdjustment");
            
            // Lights
            camLightTrs = new Transform[2];
            backLights = new List<Light>();
            auxLights = new List<Light>();
            
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
                        camLightTrs[0] = parent;
                        break;
                    case "Cam Light":
                        camLightTrs[1] = parent;
                        break;
                    case "Directional Light Back":
                    case "Back Light":
                        backLights.Add(light);
                        break;
                    case "Directional Light Fill":
                    case "Directional Light Top":
                        auxLights.Add(light);
                        break;
                }
            }
            
            // Set up panel for new buttons and insert them
            Content = new GameObject("Content", typeof(RectTransform));
            Content.transform.SetParent(LightAdj, false);
            
            var vlg = Content.AddComponent<VerticalLayoutGroup>();
            vlg.childControlHeight = false;
            vlg.childControlWidth = false;
            vlg.childForceExpandWidth = false;
            vlg.enabled = false;
            
            LightAdj.Find("DirectionHorizontal").SetParent(Content.transform, false);
            LightAdj.Find("DirectionVertical").SetParent(Content.transform, false);
            LightAdj.Find("Power").SetParent(Content.transform, false);

            var ScrollView = new GameObject("ScrollView", typeof(RectTransform));
            ScrollView.transform.SetParent(LightAdj.transform, false);

            var svScrollRect = ScrollView.AddComponent<ScrollRect>();

            var ViewPort = new GameObject("ViewPort", typeof(RectTransform));
            ViewPort.transform.SetParent(ScrollView.transform, false);
            ViewPort.AddComponent<RectMask2D>();

            var vpRectTransform = ViewPort.GetComponent<RectTransform>();

            vpRectTransform.offsetMin = new Vector2(-80, -100);
            vpRectTransform.offsetMax = new Vector2(90, 70);
            vpRectTransform.sizeDelta = new Vector2(170, 170);

            Content.transform.SetParent(ViewPort.transform, false);

            var crt = Content.GetComponent<RectTransform>();
            crt.pivot = new Vector2(0.5f, 1);

            svScrollRect.content = crt;
            svScrollRect.viewport = vpRectTransform;
            svScrollRect.horizontal = false;
            svScrollRect.scrollSensitivity = 40;
            
            crt.offsetMin = new Vector2(-85, 15);
            crt.offsetMax = new Vector2(85, 15);
            crt.sizeDelta = new Vector2(170, 0);

            foreach (var toggle in toggleInfo)
                AddBtn(Content.transform, Btn, toggle.name, toggle.resize, toggle.toggled, toggle.clickEvent);

            var csf = Content.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            vlg.enabled = true;
            
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
            }
        }

        private static void btn_BackLight(bool value)
        {
            foreach (var light in backLights.Where(light => light != null))
                light.enabled = value;
        }
        
        private static void btn_Auxiliary(bool value)
        {
            foreach (var light in auxLights.Where(light => light != null))
                light.enabled = value;
        }
        
        private static void btn_LockCamLight(bool value)
        {
            lockCamLight = value;
            
            if (lockCamLight)
            {
                for (var i = 0; i < camLightTrs.Length; i++)
                {
                    if(camLightTrs[i] == null)
                        continue;
                    
                    oldParents[i] = camLightTrs[i].parent.gameObject;

                    newParents[i] = new GameObject("CamLightLock");
                    newParents[i].SetActive(oldParents[i].activeSelf);
                    newParents[i].transform.position = oldParents[i].transform.position;
                    newParents[i].transform.eulerAngles = oldParents[i].transform.eulerAngles;

                    camLightTrs[i].parent = newParents[i].transform;
                }

                return;
            }
            
            for (var i = 0; i < camLightTrs.Length; i++)
            {
                if (oldParents[i] == null || camLightTrs[i] == null) 
                    continue;
                
                camLightTrs[i].parent = oldParents[i].transform;

                sprite.ReSetLightDir();

                Destroy(newParents[i]);
                newParents[i] = null;
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