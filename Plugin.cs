using BepInEx;
using BepInEx.Configuration;
using EFT;
using SPT.Reflection.Patching;
using System;
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
        public static ConfigEntry<float> SamplesPerFrame;
        public static ConfigEntry<float> AimNerf;

        // adv settings
        public static ConfigEntry<float> MeterMulti;
        public static ConfigEntry<float> CameraFOV;

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
            SamplesPerFrame = ConstructFloatConfig(5f, "b - Main Settings", "1-Updates per frame", "Main throttle of the mod; higher = more accurate reading / less perf", 1f, 60f);
            MeterAttenuationCoef = ConstructFloatConfig(0.5f, "b - Main Settings", "2-Light meter strength", "Determines how quickly bots can spot you per your visiblity level (100% = bots get full effect, slower recognition time)", 0f, 1f);
            AimNerf = ConstructFloatConfig(0.03f, "b - Main Settings", "3-Bot aim handicap", "Determines how much bots' aim is affected by your visibility level (higher = bots' aim more nerfed by your viz level; zero = effect is removed", 0f, 0.1f);

            // adv settings
            CameraFOV = ConstructFloatConfig(100f, "c - Advanced Settings", "CameraFOV", "Size of light camera FOV", 10f, 170f);

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
        void UpdateLightMeter()
        {
            _lightCam.fieldOfView = CameraFOV.Value;
            RepositionCamera();
            //if (!routineRunning)
            //{
            //    StartCoroutine(LightMeterRoutine());
            //}
            float score = DispatchShader();
            score /= 16f;
            score = 1f - score;
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

            _avgLightMeter = _lightMeterPool * Time.deltaTime * 100f;
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
            _lightCam.renderingPath = RenderingPath.VertexLit;
            //_lightCam.nearClipPlane = 0f;
            _lightCam.farClipPlane = 3f;
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
            _finalValueLerped = Mathf.Lerp(_finalValueLerped, finalValue, Time.deltaTime * 20f);

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
                efficiencyIndicatorStyle.fontSize = 25;
                float indicatorHorizontalPos = 20f;
                float indicatorVerticalPos = 10f;
                string input = Visualiser.GetLevelString(_finalValueLerped, false);
                //string input = Visualiser.GetLevelString(debugScore, true);
                GUI.Label(new Rect(indicatorHorizontalPos, indicatorVerticalPos, 40f, 40f), input, efficiencyIndicatorStyle);
            }
        }
    }
}