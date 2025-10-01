using BepInEx;
using BepInEx.Configuration;
using EFT;
using SPT.Reflection.Patching;
using System;
using System.Collections;
using Unity;
using UnityEngine;
using UnityEngine.Rendering;
using static EFT.ScenesPreset;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace ombarella
{
    [BepInPlugin(modGUID, modName, modVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance;

        const string modGUID = "Ombarella";
        const string modName = "Ombarella";
        const string modVersion = "0.4";

        Player _player;
        Camera _lightCam;
        RenderTexture rt;
        Texture2D tex;
        Rect rectReadPicture;
        readonly int rectSize = 4;

        public bool _isRaid { get; set; }

        public float CamDistanceOffset = 1f;
        public float CamVerticalOffset = 0.25f;
        public float AddYPlayer = 2f;
        public float LerpSpeed = 8f;

        void Awake()
        {
            Instance = this;
            Initialize();
        }
        
        // config toggles
        public static ConfigEntry<bool> MeterViz;
        public static ConfigEntry<bool> MasterSwitch;

        // settings
        public static ConfigEntry<float> MeterAttenuationCoef;
        public static ConfigEntry<float> PixelsPerFrame;

        // adv settings
        public static ConfigEntry<float> IndicatorPosX;
        public static ConfigEntry<float> IndicatorPosY;
        public static ConfigEntry<float> MeterMulti;
        public static ConfigEntry<float> CameraFOV;
        public static ConfigEntry<float> redMultiLumen;
        public static ConfigEntry<float> greenMultiLumen;
        public static ConfigEntry<float> blueMultiLumen;

        // debug values
        public static ConfigEntry<float> DebugUpdateFreq;
        public static ConfigEntry<bool> IsDebug;

        // dev
        public static ConfigEntry<float> CenterTextureScanCoef;

        void Initialize()
        {
            Utils.Logger = this.Logger;

            LoadConfig();
            SetupCamera();

            TryLoadPatch(new Patch_VisionSpeed());
        }

        void TryLoadPatch(ModulePatch patch)
        {
            try
            {
                ((ModulePatch)patch).Enable();
            }
            catch (Exception e)
            {
                string patchName = patch.ToString();
                throw;
            }
        }

        void LoadConfig()
        {
            // toggles
            MasterSwitch = ConstructBoolConfig(true, "a - Toggles", "Master Switch", "Toggle all mod functions on/off");
            MeterViz = ConstructBoolConfig(true, "a - Toggles", "Enable light meter indicator", "Visual representation of how much you are being lit and how visible you are");

            // main settings
            MeterAttenuationCoef = ConstructFloatConfig(0.3f, "b - Main Settings", "Light meter strength", "Modify how much bots are affected by the light meter (lower = you are harder to detect in low light)", 0f, 1f);
            PixelsPerFrame = ConstructFloatConfig(1f, "b - Main Settings", "Pixels scanned per frame", "Main throttle of the mod; higher = more accurate reading / less perf", 1f, 10f);

            // adv settings
            CameraFOV = ConstructFloatConfig(50f, "c - Advanced Settings", "CameraFOV", "Size of light camera FOV", 10f, 170f);
            IndicatorPosX = ConstructFloatConfig(-1000f, "c - Advanced Settings", "IndicatorPosX", "", -2000f, 2000f);
            IndicatorPosY = ConstructFloatConfig(-715f, "c - Advanced Settings", "IndicatorPosY", "", -2000f, 2000f);
            MeterMulti = ConstructFloatConfig(12f, "c - Advanced Settings", "Light Meter Multiplier", "Multiplies the base light meter reading into a normalized number", 1f, 20f);
            //CenterTextureScanCoef = ConstructFloatConfig(1f, "c - Advanced Settings", "Luminance Scan Size", "How much of the texture to scan for luminance (to focus on the player's body)", 0.1f, 1f);
            
            // color values
            redMultiLumen = ConstructFloatConfig(0.35f, "d - Color Sensitivity", "Red multiplier", "During pixel analysis red is multiplied by this to discern lumenance", 0, 1f);
            greenMultiLumen = ConstructFloatConfig(0.86f, "d - Color Sensitivity", "Green multiplier", "During pixel analysis green is multiplied by this to discern lumenance", 0, 1f);
            blueMultiLumen = ConstructFloatConfig(0.2f, "d - Color Sensitivity", "Blue multiplier", "During pixel analysis blue is multiplied by this to discern lumenance", 0, 1f);

            // debug
            IsDebug = ConstructBoolConfig(false, "y - Debug", "1) Enable debug logging", "");
            DebugUpdateFreq = ConstructFloatConfig(1f, "y - Debug", "2) Debug updates per second", "How frequently the debug logger updates per second", 1f, 10f);

            // dev
        }

        void Update()
        {
            if (!MasterSwitch.Value)
            {
                return;
            }
            PluginManager.Update();
            Utils.Update(Time.deltaTime);

            if (!_isRaid)
            {
                return;
            }
            if (_player == null)
            {
                _player = Utils.GetMainPlayer();
            }
            if (_player == null)
            {
                Utils.LogError("Unable to return player, meter updates aborted");
                return;
            }
            //
            // good to update meter
            //
            UpdateLightMeter();
            CameraController.UpdateDebugLines();
        }

        public void CleanupRaid()
        {
            _player = null;
            _isRaid = false;
        }

        public void StartRaid()
        {
            _player = Utils.GetMainPlayer();
            _isRaid = true;
        }

        void UpdateLightMeter()
        {
            _lightCam.fieldOfView = CameraFOV.Value;
            RepositionCamera();
            if (!routineRunning)
            {
                StartCoroutine(LightMeterRoutine());
            }
        }

        bool routineRunning = false;

        IEnumerator LightMeterRoutine()
        {
            routineRunning = true;
            float processTime = 0;

            _lightCam.Render();

            RenderTexture.active = rt;

            tex.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0, false);
            tex.Apply(false);

            Color[] allColors = tex.GetPixels();
            int i = 0;

            float totalLuminance = 0f;

            int pixelsThisFrame = 0;

            float highR = 0;
            float lowR = 1f;
            float highG = 0;
            float lowG = 1f;
            float highB = 0;
            float lowB = 1f;

            while (i < allColors.Length)
            {
                Color color = allColors[i];

                totalLuminance += (color.r * redMultiLumen.Value) + (color.g * greenMultiLumen.Value) + (color.b * blueMultiLumen.Value);

                if (color.r > highR)
                {
                    highR = color.r;
                }
                if (color.r < lowR)
                {
                    lowR = color.r;
                }

                if (color.g > highG)
                {
                    highG = color.g;
                }
                if (color.g < lowG)
                {
                    lowG = color.g;
                }

                if (color.b > highB)
                {
                    highB = color.b;
                }
                if (color.b < lowB)
                {
                    lowB = color.b;
                }

                pixelsThisFrame++;

                if (pixelsThisFrame == PixelsPerFrame.Value)
                {
                    pixelsThisFrame = 0;
                    yield return new WaitForEndOfFrame();
                    processTime += Time.deltaTime;
                }

                processTime += Time.deltaTime;
                i++;
            }

            float averageLuminance = totalLuminance / allColors.Length;
            float averageColorBreadth = GetColorBreadth(highR, lowR, highG, lowG, highB, lowB);

            Utils.Log($"lumen {averageLuminance}", false);

            RecalcMeterAverage(averageLuminance, averageColorBreadth, processTime);
            routineRunning = false;
        }

        float GetColorBreadth(float highR, float lowR, float highG, float lowG, float highB, float lowB)
        {
            float rSize = highR - lowR;
            float gSize = highG - lowG;
            float bSize = highB - lowB;

            rSize *= 0.8f;
            gSize *= 0.3f;
            bSize *= 1.0f;

            float totalSize = (rSize + gSize + bSize) / 3f;
            Utils.Log($"color breadth total size : {totalSize}", false);
            return totalSize;
        }

        void RecalcMeterAverage(float lumen, float colorBreadth, float processTime)
        {
            lumen = Mathf.Clamp(lumen, 0.01f, 0.1f);
            lumen *= MeterMulti.Value;
            float flagrancy = (lumen + colorBreadth) / 2f;
            _lightMeterPool -= _avgLightMeter;
            _lightMeterPool = Mathf.Clamp(_lightMeterPool, 0.01f, 10f);
            //_lightMeterPool += lumen;
            _lightMeterPool += flagrancy;
            _avgLightMeter = _lightMeterPool * processTime;
            ClampFinalValue();
        }

        ConfigEntry<float> ConstructFloatConfig(float defaultValue, string category, string descriptionShort, string descriptionFull, float min, float max)
        {
            ConfigEntry<float> result = ((BaseUnityPlugin)this).Config.Bind<float>(category, descriptionShort, defaultValue, new ConfigDescription(descriptionFull, (AcceptableValueBase)(object)new AcceptableValueRange<float>(min, max), Array.Empty<object>()));
            return result;
        }

        ConfigEntry<bool> ConstructBoolConfig(bool defaultValue, string category, string descriptionShort, string descriptionFull)
        {
            ConfigEntry<bool> result = ((BaseUnityPlugin)this).Config.Bind<bool>(category, descriptionShort, defaultValue, new ConfigDescription(descriptionFull, (AcceptableValueBase)null, Array.Empty<object>()));
            return result;
        }
        private void SetupCamera()
        {
            _lightCam = new Camera();
            _lightCam = gameObject.AddComponent<Camera>();
            rt = new RenderTexture(rectSize, rectSize, 1);
            rt.dimension = TextureDimension.Tex2D;
            rt.wrapMode = TextureWrapMode.Clamp;
            _lightCam.targetTexture = rt;
            tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBAHalf, false);
            rectReadPicture = new Rect(0, 0, rt.width, rt.height);
            CameraController.Initialize(_lightCam);
            _lightCam.enabled = false;
        }

        float _lightMeterPool = 0f;
        float _avgLightMeter = 0.01f;
        public float FinalLightMeter = 0.01f;


        float _finalValueLerped = 0.01f;

        void ClampFinalValue()
        {
            float finalValue = Mathf.Clamp(_avgLightMeter, 0.01f, 1f);
            _finalValueLerped = Mathf.Lerp(_finalValueLerped, finalValue, Time.deltaTime * LerpSpeed);

            FinalLightMeter = Mathf.Lerp(_finalValueLerped, 1f, MeterAttenuationCoef.Value);
            Utils.Log($"_finalValueBeforeMod : {_finalValueLerped} // final light output : {FinalLightMeter}", false);
        }

        void RepositionCamera()
        {
            CameraController.RepositionCamera();
        }

        GUIStyle efficiencyIndicatorStyle = new GUIStyle();

        void OnGUI()
        {
            if (!MeterViz.Value || !MasterSwitch.Value)
            { 
                return; 
            }
            if (Utils.IsInRaid())
            {
                efficiencyIndicatorStyle.normal.textColor = Color.grey;
                efficiencyIndicatorStyle.fontSize = 30;
                float indicatorHorizontalPos = (float)Screen.width / 2f + IndicatorPosX.Value;
                float indicatorVerticalPos = (float)Screen.height / 2f + IndicatorPosY.Value;
                string input = Visualiser.GetLevelString(_finalValueLerped);
                GUI.Label(new Rect(indicatorHorizontalPos, indicatorVerticalPos, 40f, 40f), input, efficiencyIndicatorStyle);
            }
        }
    }
}