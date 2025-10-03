using BepInEx;
using BepInEx.Configuration;
using EFT;
using SPT.Reflection.Patching;
using System;
using System.Collections;
using System.IO;
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
        RenderTexture _rt;
        Texture2D _tex;
        Rect _rectReadPicture;
        readonly int _texSize = 16;

        public bool IsRaid { get; set; }


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
        public static ConfigEntry<float> AimNerf;

        // adv settings
        public static ConfigEntry<float> IndicatorPosX;
        public static ConfigEntry<float> IndicatorPosY;
        public static ConfigEntry<float> MeterMulti;
        public static ConfigEntry<float> CameraFOV;
        public static ConfigEntry<float> redMultiLumen;
        public static ConfigEntry<float> greenMultiLumen;
        public static ConfigEntry<float> blueMultiLumen;
        public static ConfigEntry<float> redMultiBreadt;
        public static ConfigEntry<float> greenMultiBreadt;
        public static ConfigEntry<float> blueMultiBreadt;

        // camera rig settings
        public static ConfigEntry<float> CamHorizontalOffset;
        public static ConfigEntry<float> CamVerticalOffset;
        public static ConfigEntry<float> PlayerVerticalOffset;
        public static ConfigEntry<float> LerpSpeed;

        // debug values
        public static ConfigEntry<float> DebugUpdateFreq;
        public static ConfigEntry<bool> IsDebug;

        // dev
        public static ConfigEntry<float> CullingMask;
        public static ConfigEntry<float> OutputMulti;

        void Initialize()
        {
            Utils.Logger = this.Logger;
            LoadPatches();
            LoadConfig();
            //SetupCamera();
            PopulateShader();
            SetupRenderTexture();
            MapRenderTexToShader();
        }

        void LoadPatches()
        {
            TryLoadPatch(new Patch_VisionSpeed());
            TryLoadPatch(new Patch_AimOffset());
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
            PixelsPerFrame = ConstructFloatConfig(1f, "b - Main Settings", "1-Pixels scanned per frame", "Main throttle of the mod; higher = more accurate reading / less perf", 1f, 60f);
            MeterAttenuationCoef = ConstructFloatConfig(0.75f, "b - Main Settings", "2-Light meter strength", "Determines how quickly bots can spot you per your visiblity level (100% = bots get full effect, slower recognition time)", 0f, 1f);
            AimNerf = ConstructFloatConfig(0.3f, "b - Main Settings", "3-Bot aim handicap", "Determines how much bots' aim is affected by your visibility level (100% = full nerf, bots' aim heavily affected by viz level", 0f, 1f);

            // adv settings
            CameraFOV = ConstructFloatConfig(50f, "c - Advanced Settings", "CameraFOV", "Size of light camera FOV", 10f, 170f);
            IndicatorPosX = ConstructFloatConfig(-1000f, "c - Advanced Settings", "IndicatorPosX", "", -2000f, 2000f);
            IndicatorPosY = ConstructFloatConfig(-715f, "c - Advanced Settings", "IndicatorPosY", "", -2000f, 2000f);
            MeterMulti = ConstructFloatConfig(12f, "c - Advanced Settings", "Light Meter Multiplier", "Multiplies the base light meter reading into a normalized number", 1f, 20f);
            LerpSpeed = ConstructFloatConfig(8f, "c - Advanced Settings", "Meter lerp speed", "How fast the light meter updates its output", 1f, 20f);

            // color values
            redMultiLumen = ConstructFloatConfig(0.35f, "d - Color Sensitivity", "1-Red lumen multiplier", "Red in pixel is multiplied by this value to calculate lumenance", 0, 1f);
            greenMultiLumen = ConstructFloatConfig(0.86f, "d - Color Sensitivity", "2-Green lumen multiplier", "Green in pixel is multiplied by this value to calculate lumenance", 0, 1f);
            blueMultiLumen = ConstructFloatConfig(0.2f, "d - Color Sensitivity", "3-Blue lumen multiplier", "Blue in pixel is multiplied by this value to calculate lumenance", 0, 1f);

            redMultiBreadt = ConstructFloatConfig(0.75f, "d - Color Sensitivity", "4-Red breadth multiplier", "Red range in pixel is multiplied by this value to calculate breadth (camo)", 0, 1f);
            greenMultiBreadt = ConstructFloatConfig(0.3f, "d - Color Sensitivity", "5-Green breadth multiplier", "Blue range in pixel is multiplied by this value to calculate breadth (camo)", 0, 1f);
            blueMultiBreadt = ConstructFloatConfig(1f, "d - Color Sensitivity", "6-Blue breadth multiplier", "Green range in pixel is multiplied by this value to calculate breadth (camo)", 0, 1f);

            // camera rig
            CamHorizontalOffset = ConstructFloatConfig(1f, "e - Camera Rig Settings", "Camera horizontal offset", "Distance between the camera and the player focus point on horizontal plane", 0.1f, 3f);
            CamVerticalOffset = ConstructFloatConfig(0.25f, "e - Camera Rig Settings", "Camera vertical offset", "Distance between the camera and the player focus point on vertical plane", 0.1f, 3f);
            PlayerVerticalOffset = ConstructFloatConfig(2f, "e - Camera Rig Settings", "Player vertical offset", "Vertical distance between the player's root position (feet) and the camera focus point", 0.1f, 3f);

            // debug
            IsDebug = ConstructBoolConfig(false, "y - Debug", "1) Enable debug logging", "");
            DebugUpdateFreq = ConstructFloatConfig(1f, "y - Debug", "2) Debug updates per second", "How frequently the debug logger updates per second", 1f, 10f);

            // dev
            CullingMask = ConstructFloatConfig(1f, "z - Dev", "CullingMask", "", 0f, 100f);
            OutputMulti = ConstructFloatConfig(1f, "z - Dev", "OutputMulti", "", 0f, 100f);
        }

        float updateTimer = 0f;

        void Update()
        {
            if (!MasterSwitch.Value)
            {
                return;
            }
            PluginManager.Update();
            Utils.Update(Time.deltaTime);

            if (!IsRaid)
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
            updateTimer += Time.deltaTime;
            if (updateTimer > 1f / PixelsPerFrame.Value)
            {
                updateTimer = 0;
                UpdateLightMeter();
            }
            //CameraRig.UpdateDebugLines();
        }

        public void CleanupRaid()
        {
            _player = null;
            IsRaid = false;
        }

        public void StartRaid()
        {
            _player = Utils.GetMainPlayer();
            IsRaid = true;
        }

        float debugScore = 0f;
        void UpdateLightMeter()
        {
            _lightCam.depth = CullingMask.Value;
            _lightCam.fieldOfView = CameraFOV.Value;
            RepositionCamera();
            //if (!routineRunning)
            //{
            //    StartCoroutine(LightMeterRoutine());
            //}
            float score = DispatchShader();
            score /= 64f;
            score *= OutputMulti.Value;
            Utils.Log($"final light score is {score}", true);
            debugScore = score;
        }

        bool routineRunning = false;

        IEnumerator LightMeterRoutine()
        {
            routineRunning = true;
            float processTime = 0;

            _lightCam.Render();

            RenderTexture.active = _rt;

            _tex.ReadPixels(new Rect(0, 0, _tex.width, _tex.height), 0, 0, false);
            _tex.Apply(false);

            //Color[] allColors = _tex.GetPixels();

            var allColors = _tex.GetPixelData<Color32>(0);

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

                if (pixelsThisFrame >= Mathf.RoundToInt(PixelsPerFrame.Value))
                {
                    pixelsThisFrame = 0;
                    yield return new WaitForEndOfFrame();
                    processTime += Time.deltaTime;
                }
                else
                {
                    processTime += Time.deltaTime;
                }
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

            rSize *= redMultiBreadt.Value;
            gSize *= greenMultiBreadt.Value;
            bSize *= blueMultiBreadt.Value;

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
            _lightMeterPool += flagrancy;
            _lightMeterPool = Mathf.Clamp(_lightMeterPool, 0.01f, 10f);

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
            _rt = new RenderTexture(_texSize, _texSize, 1);
            _rt.dimension = TextureDimension.Tex2D;
            _rt.wrapMode = TextureWrapMode.Clamp;
            _lightCam.targetTexture = _rt;
            _tex = new Texture2D(_rt.width, _rt.height, TextureFormat.RGBAHalf, false);
            _rectReadPicture = new Rect(0, 0, _rt.width, _rt.height);
            CameraRig.Initialize(_lightCam);
            _lightCam.enabled = false;
        }

        ComputeShader shader;

        void PopulateShader()
        {
            var bundle = AssetBundle.LoadFromFile(Path.Combine(BepInEx.Paths.PluginPath, "Ombarella", "ombhistogram"));
            shader = bundle.LoadAsset<ComputeShader>("GetAllPixelColors");
            string isNull = shader == null ? "is NULL" : "is loaded!";
            Debug.Log($"shader {isNull}");
        }

        void SetupRenderTexture()
        {
            _rt = new RenderTexture(_texSize, _texSize, 4, RenderTextureFormat.RGB565);
            _rt.enableRandomWrite = true;
            _rt.Create();

            _lightCam = new Camera();
            _lightCam = gameObject.AddComponent<Camera>();
            _lightCam.targetTexture = _rt;
            _lightCam.renderingPath = RenderingPath.DeferredShading;
            CameraRig.Initialize(_lightCam);

            _tex = new Texture2D(_rt.width, _rt.height, TextureFormat.RGB565, false);
            _tex.Apply();
        }

        float DispatchShader()
        {
            Graphics.CopyTexture(_rt, _tex);
            //https://youtu.be/4Wh8GRrz7WA?t=705
            //shader.Dispatch(_kernelID, _texSize / 8, _texSize / 8, 1);
            shader.Dispatch(_handleInitialize, _texSize, 1, 1);
            shader.Dispatch(_handleMain, (_tex.width + 7) / 8, (_tex.height + 7) / 8, 1);
            _histogramBuffer.GetData(_histogramData);

            float finalScore = 0;
            for (int i = 0; i < _histogramData.Length; i++)
            {
                //Debug.Log(_histogramData[i]);
                finalScore += _histogramData[i];
            }
            finalScore /= _histogramData.Length;
            Debug.Log($"final score : {finalScore}");
            //return 1f - (finalScore / 64f);
            return finalScore;
        }

        int _handleInitialize;
        int _handleMain;
        ComputeBuffer _histogramBuffer;
        public uint[] _histogramData;

        void MapRenderTexToShader()
        {
            _handleInitialize = shader.FindKernel("HistogramInitialize");
            _handleMain = shader.FindKernel("HistogramMain");
            _histogramBuffer = new ComputeBuffer(_texSize, sizeof(uint) * 4);
            _histogramData = new uint[_texSize * 4];


            shader.SetTexture(_handleMain, "InputTexture", _tex);
            shader.SetBuffer(_handleMain, "HistogramBuffer", _histogramBuffer);
            shader.SetBuffer(_handleInitialize, "HistogramBuffer", _histogramBuffer);
        }

        float _lightMeterPool = 0f;
        float _avgLightMeter = 0.01f;
        public float FinalLightMeter = 0.01f;


        float _finalValueLerped = 0.01f;

        void ClampFinalValue()
        {
            float finalValue = Mathf.Clamp(_avgLightMeter, 0.01f, 1f);
            _finalValueLerped = Mathf.Lerp(_finalValueLerped, finalValue, Time.deltaTime * LerpSpeed.Value);

            float meterCoef = 1f - MeterAttenuationCoef.Value;
            FinalLightMeter = Mathf.Lerp(_finalValueLerped, 1f, meterCoef);
            Utils.Log($"_finalValueBeforeMod : {_finalValueLerped} // final light output : {FinalLightMeter}", false);
        }

        void RepositionCamera()
        {
            CameraRig.RepositionCamera();
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
                efficiencyIndicatorStyle.fontSize = 15;
                float indicatorHorizontalPos = 20f;
                float indicatorVerticalPos = 0;
                //string input = Visualiser.GetLevelString(_finalValueLerped, false);
                string input = Visualiser.GetLevelString(debugScore, true);
                GUI.Label(new Rect(indicatorHorizontalPos, indicatorVerticalPos, 40f, 40f), input, efficiencyIndicatorStyle);
            }
        }
    }
}