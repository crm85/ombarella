using BepInEx;
using BepInEx.Configuration;
using EFT;
using SPT.Reflection.Patching;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

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
        readonly int _texSize = 32;

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
        public static ConfigEntry<float> SamplesPerFrame;
        public static ConfigEntry<float> AimNerf;

        // adv settings
        public static ConfigEntry<float> CameraFOV;
        public static ConfigEntry<float> LumaCoef;

        // color settings
        public static ConfigEntry<float> RedLumaMulti;
        public static ConfigEntry<float> GreenLumaMulti;
        public static ConfigEntry<float> BlueLumaMulti;

        public static ConfigEntry<float> RedBreadthMulti;
        public static ConfigEntry<float> GreenBreadthMulti;
        public static ConfigEntry<float> BlueBreadthMulti;


        // camera rig settings
        public static ConfigEntry<float> CamHorizontalOffset;
        public static ConfigEntry<float> CamVerticalOffset;
        public static ConfigEntry<float> PlayerVerticalOffset;
        public static ConfigEntry<float> LerpSpeed;

        // debug values
        public static ConfigEntry<float> DebugUpdateFreq;
        public static ConfigEntry<bool> IsDebug;

        // dev
        public static ConfigEntry<float> dev1;
        public static ConfigEntry<float> dev2;

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
            SamplesPerFrame = ConstructFloatConfig(2f, "b - Main Settings", "1-Updates per frame", "Main throttle of the mod; higher = more accurate reading / less perf", 1f, 60f);
            MeterAttenuationCoef = ConstructFloatConfig(0.5f, "b - Main Settings", "2-Light meter strength", "Determines how quickly bots can spot you per your visiblity level (100% = bots get full effect, slower recognition time)", 0f, 1f);
            AimNerf = ConstructFloatConfig(0.03f, "b - Main Settings", "3-Bot aim handicap", "Determines how much bots' aim is affected by your visibility level (higher = bots' aim more nerfed by your viz level; zero = effect is removed", 0f, 0.1f);

            // adv settings
            CameraFOV = ConstructFloatConfig(70f, "c - Advanced Settings", "CameraFOV", "Size of light camera FOV", 10f, 170f);
            LumaCoef = ConstructFloatConfig(2f, "c - Advanced Settings", "Luma main multiplier", "Multiplies the raw luma reading into a normalized number", 1f, 20f);

            // color multis
            RedLumaMulti = ConstructFloatConfig(0.21f, "d - Color Settings", "1-Red luma multi", "Red color in pixel analysis is multiplied by this to produce the luma calculation", 0f, 1f);
            GreenLumaMulti = ConstructFloatConfig(0.71f, "d - Color Settings", "2-Green luma multi", "Green color in pixel analysis is multiplied by this to produce the luma calculation", 0f, 1f);
            BlueLumaMulti = ConstructFloatConfig(0.072f, "d - Color Settings", "3-Blue luma multi", "Blue color in pixel analysis is multiplied by this to produce the luma calculation", 0f, 1f);

            RedBreadthMulti = ConstructFloatConfig(1f, "d - Color Settings", "4-Red breadth multi", "Red color in pixel analysis is multiplied by this to produce the breadth calculation", 0f, 1f);
            GreenBreadthMulti = ConstructFloatConfig(1f, "d - Color Settings", "5-Green breadth multi", "Green color in pixel analysis is multiplied by this to produce the breadth calculation", 0f, 1f);
            BlueBreadthMulti = ConstructFloatConfig(1f, "d - Color Settings", "6-Blue breadth multi", "Blue color in pixel analysis is multiplied by this to produce the breadth calculation", 0f, 1f);


            // camera rig
            CamHorizontalOffset = ConstructFloatConfig(0.7f, "e - Camera Rig Settings", "Camera horizontal offset", "Distance between the camera and the player focus point on horizontal plane", 0.1f, 3f);
            CamVerticalOffset = ConstructFloatConfig(0.1f, "e - Camera Rig Settings", "Camera vertical offset", "Distance between the camera and the player focus point on vertical plane", 0.1f, 3f);
            PlayerVerticalOffset = ConstructFloatConfig(1f, "e - Camera Rig Settings", "Player vertical offset", "Vertical distance between the player's root position (feet) and the camera focus point", 0.1f, 3f);

            // debug
            IsDebug = ConstructBoolConfig(false, "y - Debug", "1) Enable debug logging", "");
            DebugUpdateFreq = ConstructFloatConfig(1f, "y - Debug", "2) Debug updates per second", "How frequently the debug logger updates per second", 1f, 10f);

            // dev
            //dev1 = ConstructFloatConfig(1f, "z - Dev", "dev1", "", 0f, 100f);
            //dev2 = ConstructFloatConfig(1f, "z - Dev", "dev2", "", 0f, 100f);
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
            if (updateTimer > 1f / SamplesPerFrame.Value)
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
        float debugScore2 = 0f;
        void UpdateLightMeter()
        {
            _lightCam.fieldOfView = CameraFOV.Value;
            List<Player> playersList = Utils.GetAllPlayers();
            if (playersList.Count == 0)
            {
                return;
            }
            CameraRig.RepositionCamera(playersList);
            //if (!routineRunning)
            //{
            //    StartCoroutine(LightMeterRoutine());
            //}
            float score = DispatchShader();
            score *= LumaCoef.Value;
            //score *= OutputMulti.Value;
            debugScore = score;
            RecalcMeterAverage(score);
        }

        bool routineRunning = false;

        void RecalcMeterAverage(float lumen)
        {
            _lightMeterPool -= _avgLightMeter;
            _lightMeterPool += lumen;
            _lightMeterPool = Mathf.Clamp(_lightMeterPool, 0.01f, 10f);

            _avgLightMeter = _lightMeterPool * Time.deltaTime * 50f;
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
            _rt = new RenderTexture(_texSize, _texSize, 1, RenderTextureFormat.RGB565);
            _rt.dimension = TextureDimension.Tex2D;
            _rt.wrapMode = TextureWrapMode.Clamp;
            _lightCam.targetTexture = _rt;
            _rectReadPicture = new Rect(0, 0, _rt.width, _rt.height);
            CameraRig.Initialize(_lightCam);
            _lightCam.enabled = false;
        }

        ComputeShader _computeShader;

        void PopulateShader()
        {
            var bundle = AssetBundle.LoadFromFile(Path.Combine(BepInEx.Paths.PluginPath, "Ombarella", "shader"));
            _computeShader = bundle.LoadAsset<ComputeShader>("GetAllPixelColors");
            string isNull = _computeShader == null ? "is NULL" : "is loaded!";
            Debug.Log($"shader {isNull}");
        }

        private ComputeBuffer outputBuffer;

        void SetupRenderTexture()
        {
            _rt = new RenderTexture(_texSize, _texSize, 4, RenderTextureFormat.RGB565);
            _rt.enableRandomWrite = true;
            _rt.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R32G32B32A32_SFloat;
            _rt.depth = 24;
            _rt.stencilFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.None;
            _rt.dimension = TextureDimension.Tex2D;
            _rt.Create();

            _lightCam = new Camera();
            _lightCam = gameObject.AddComponent<Camera>();
            _lightCam.targetTexture = _rt;
            _lightCam.renderingPath = RenderingPath.DeferredShading;
            //_lightCam.cullingMask = Utils.GetPlayerCullingMask();
            //_lightCam.nearClipPlane = 0f;
            //_lightCam.farClipPlane = 3f;
            CameraRig.Initialize(_lightCam);


            //_rt = new RenderTexture(_texSize, _texSize, 0, RenderTextureFormat.RGB565);
            //_rt.enableRandomWrite = true;
            //_rt.Create();

            //// Assign RenderTexture to the camera
            //_lightCam.targetTexture = _rt;

            // Prepare output buffer
            outputColors = new Color[_texSize * _texSize];
            outputBuffer = new ComputeBuffer(outputColors.Length, sizeof(float) * 4);

            // Set kernel handle for compute shader
            _handleMain = _computeShader.FindKernel("CSMain");

            _computeShader.SetTexture(_handleMain, "textureInput", _rt);
            _computeShader.SetBuffer(_handleMain, "outputBuffer", outputBuffer);
        }

        Color[] outputColors;

        float DispatchShader()
        {
            _computeShader.Dispatch(_handleMain, _texSize / 8, _texSize / 8, 1);
            outputBuffer.GetData(outputColors);

            float luma = GetLuma(outputColors);
            float breadth = GetBreadth(outputColors);
            float result = (luma + breadth) / 2f;
            return result;
        }

        int _handleInitialize;
        int _handleMain;
        ComputeBuffer _histogramBuffer;
        public uint[] _histogramData;

        void MapRenderTexToShader()
        {
            //_handleMain = _computeShader.FindKernel("CSMain");
            //_histogramBuffer = new ComputeBuffer(_texSize, sizeof(uint) * 4);
            //_histogramData = new uint[_texSize * 4];


            //_computeShader.SetTexture(_handleMain, "InputTexture", _tex);
            //_computeShader.SetBuffer(_handleMain, "HistogramBuffer", _histogramBuffer);
            //_computeShader.SetBuffer(_handleInitialize, "HistogramBuffer", _histogramBuffer);
        }

        float _lightMeterPool = 0f;
        float _avgLightMeter = 0.01f;
        public float FinalLightMeter = 0.01f;


        float _finalValueLerped = 0.01f;

        void ClampFinalValue()
        {
            float finalValue = Mathf.Clamp(_avgLightMeter, 0.01f, 1f);
            _finalValueLerped = Mathf.Lerp(_finalValueLerped, finalValue, Time.deltaTime * 20f);

            float meterCoef = 1f - MeterAttenuationCoef.Value;
            FinalLightMeter = Mathf.Lerp(_finalValueLerped, 1f, meterCoef);
            Utils.Log($"_finalValueBeforeMod : {_finalValueLerped} // final light output : {FinalLightMeter}", false);
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
                efficiencyIndicatorStyle.fontSize = 20;
                float indicatorHorizontalPos = 20f;
                float indicatorVerticalPos = 10f;
                string input = Visualiser.GetLevelString(_finalValueLerped, false);
                GUI.Label(new Rect(indicatorHorizontalPos, indicatorVerticalPos, 40f, 40f), input, efficiencyIndicatorStyle);
                if (IsDebug.Value)
                {
                    string debugString = string.Format($"luma is {debugScore}, breadth is {debugScore2}");
                    GUI.Label(new Rect(indicatorHorizontalPos, indicatorVerticalPos + 40f, 40f, 40f), debugString, efficiencyIndicatorStyle);
                }
            }
        }




        float GetLuma(Color[] pixels)
        {
            //0.2126729, 0.7151522, 0.0721750

            float rCoef = RedLumaMulti.Value;
            float gCoef = BlueLumaMulti.Value;
            float bCoef = GreenLumaMulti.Value;

            float r = 0;
            float g = 0;
            float b = 0;

            for (int i = 0; i < pixels.Length; i++)
            {
                r += pixels[i].r * rCoef;
                g += pixels[i].g * gCoef;
                b += pixels[i].b * bCoef;
            }

            r /= pixels.Length;
            g /= pixels.Length;
            b /= pixels.Length;

            float luma = r + g + b;
            return luma;
        }

        float GetBreadth(Color[] pixels)
        {
            float rLow = 1f;
            float gLow = 1f;
            float bLow = 1f;

            float rHigh = 0;
            float gHigh = 0;
            float bHigh = 0;

            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].r < rLow) rLow = pixels[i].r;
                if (pixels[i].g < gLow) gLow = pixels[i].g;
                if (pixels[i].b < bLow) bLow = pixels[i].b;

                if (pixels[i].r > rHigh) rHigh = pixels[i].r;
                if (pixels[i].g > gHigh) gHigh = pixels[i].g;
                if (pixels[i].b > bHigh) bHigh = pixels[i].b;
            }

            float rRange = rHigh - rLow;
            float gRange = gHigh - gLow;
            float bRange = bHigh - bLow;

            rRange *= RedBreadthMulti.Value;
            gRange *= GreenBreadthMulti.Value;
            bRange *= BlueBreadthMulti.Value;

            float breadth = rRange + gRange + bRange;
            breadth /= 3f;
            debugScore2 = breadth;
            return breadth;
        }
    }
}