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
        int _texSize = 32;
        bool _isDestroyed;
        bool _asyncReadbackPending;
        bool _asyncScoreReady;
        float _asyncScore = 0.01f;
        int _asyncBatchId;
        readonly Dictionary<Player, Renderer[]> _playerRendererCache = new Dictionary<Player, Renderer[]>();

        public bool IsRaid { get; set; }


        void Awake()
        {
            Instance = this;
            Initialize();
        }

        void OnDestroy()
        {
            _isDestroyed = true;
            ReleaseRenderResources();
        }
        
        // config toggles
        public static ConfigEntry<bool> MeterViz;
        public static ConfigEntry<bool> MasterSwitch;
        public static ConfigEntry<bool> UseLuma;
        public static ConfigEntry<bool> UseFikaPlayerAveraging;

        // luma settings

        // breadth settings

        // settings
        public static ConfigEntry<float> MeterAttenuationCoef;
        public static ConfigEntry<float> SamplesPerSec;
        public static ConfigEntry<float> AimNerf;

        // adv settings
        public static ConfigEntry<float> CameraFOV;
        public static ConfigEntry<float> LumaCoef;
        public static ConfigEntry<float> RenderTextureResolution;
        public static ConfigEntry<bool> UseAsyncGPUReadback;

        // color settings
        public static ConfigEntry<float> RedLumaMulti;
        public static ConfigEntry<float> GreenLumaMulti;
        public static ConfigEntry<float> BlueLumaMulti;

        public static ConfigEntry<float> RedBreadthMulti;
        public static ConfigEntry<float> GreenBreadthMulti;
        public static ConfigEntry<float> BlueBreadthMulti;


        // camera rig settings
        public static ConfigEntry<float> CamHorizontalOffset;
        public static ConfigEntry<bool> RenderPlayerOnly;
        public static ConfigEntry<bool> ForceTargetPlayerRenderers;

        // debug values
        public static ConfigEntry<float> DebugUpdateFreq;
        public static ConfigEntry<bool> IsDebug;
        public static ConfigEntry<bool> ShowRenderTexturePreview;
        public static ConfigEntry<float> RenderTexturePreviewSize;

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
                Logger.LogError($"Failed to load patch {patchName}: {e}");
                throw;
            }
        }

        void LoadConfig()
        {
            // toggles
            MasterSwitch = ConstructBoolConfig(true, "a - Toggles", "Master Switch", "Toggle all mod functions on/off");
            MeterViz = ConstructBoolConfig(true, "a - Toggles", "Enable light meter indicator", "Visual representation of how much you are being lit and how visible you are");
            UseLuma = ConstructBoolConfig(true, "a - Toggles", "Use Luma meter", "Toggle to incorportate 'luma' analysis, a general measure of your character's brightness");
            UseFikaPlayerAveraging = ConstructBoolConfig(false, "a - Toggles", "Use Fika player averaging", "When enabled, target all real non-headless Fika client players and average each player's visibility from their nearest bot. Safe to leave disabled when Fika is not installed.");

            // main settings
            SamplesPerSec = ConstructFloatConfig(1f, "b - Main Settings", "1-Light samples per second", "Main throttle of the mod; higher = more accurate reading / less perf", 1f, 60f);
            MeterAttenuationCoef = ConstructFloatConfig(1f, "b - Main Settings", "2-Light meter strength", "Determines how quickly bots can spot you per your visiblity level (100% = bots get full effect, slower recognition time)", 0f, 1f);
            AimNerf = ConstructFloatConfig(0.03f, "b - Main Settings", "3-Bot aim handicap", "Determines how much bots' aim is affected by your visibility level (higher = bots' aim more nerfed by your viz level; zero = effect is removed", 0f, 0.1f);

            // adv settings
            CameraFOV = ConstructFloatConfig(70f, "c - Advanced Settings", "CameraFOV", "Size of light camera FOV", 10f, 170f);
            LumaCoef = ConstructFloatConfig(6f, "c - Advanced Settings", "Luma coefficient", "Multiplies the luma result", 1f, 20f);
            RenderTextureResolution = ConstructFloatConfig(64f, "c - Advanced Settings", "Render texture resolution", "Resolution of the light camera render texture. Applied before raid start and rounded to the nearest multiple of 8.", 16f, 512f);
            UseAsyncGPUReadback = ConstructBoolConfig(true, "c - Advanced Settings", "Use async GPU readback", "Avoids blocking the main thread while reading the light camera texture. Disable to use the old synchronous compute readback path.");

            // color multis
            // traditional luma values : r 0.2126729, g 0.7151522, b 0.0721750
            RedLumaMulti = ConstructFloatConfig(0.79f, "d - Color Settings", "1-Red luma multi", "Red color in pixel analysis is multiplied by this to produce the luma calculation", 0f, 1f);
            GreenLumaMulti = ConstructFloatConfig(0.29f, "d - Color Settings", "2-Green luma multi", "Green color in pixel analysis is multiplied by this to produce the luma calculation", 0f, 1f);
            BlueLumaMulti = ConstructFloatConfig(0.93f, "d - Color Settings", "3-Blue luma multi", "Blue color in pixel analysis is multiplied by this to produce the luma calculation", 0f, 1f);

            RedBreadthMulti = ConstructFloatConfig(0.79f, "d - Color Settings", "4-Red breadth multi", "Red color in pixel analysis is multiplied by this to produce the breadth calculation", 0f, 1f);
            GreenBreadthMulti = ConstructFloatConfig(0.29f, "d - Color Settings", "5-Green breadth multi", "Green color in pixel analysis is multiplied by this to produce the breadth calculation", 0f, 1f);
            BlueBreadthMulti = ConstructFloatConfig(0.93f, "d - Color Settings", "6-Blue breadth multi", "Blue color in pixel analysis is multiplied by this to produce the breadth calculation", 0f, 1f);


            // camera rig
            CamHorizontalOffset = ConstructFloatConfig(4f, "e - Camera Rig Settings", "Camera horizontal offset", "Distance between the camera and the player focus point on horizontal plane", 0.1f, 5f);
            RenderPlayerOnly = ConstructBoolConfig(true, "e - Camera Rig Settings", "Render player only", "When enabled, the light camera renders the target player against a black background");
            ForceTargetPlayerRenderers = ConstructBoolConfig(true, "e - Camera Rig Settings", "Force target player renderers", "Temporarily forces the sampled player's renderers visible and renderable by the light camera, then restores them");

            // debug
            IsDebug = ConstructBoolConfig(false, "y - Debug", "1) Enable debug logging", "");
            DebugUpdateFreq = ConstructFloatConfig(1f, "y - Debug", "2) Debug updates per second", "How frequently the debug logger updates per second", 1f, 10f);
            ShowRenderTexturePreview = ConstructBoolConfig(false, "y - Debug", "3) Show render texture preview", "Draws the light-meter render texture in the game window for debugging");
            RenderTexturePreviewSize = ConstructFloatConfig(256f, "y - Debug", "4) Render texture preview size", "Size of the render texture debug preview in pixels", 64f, 512f);

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
            if (_player == null && !UseFikaPlayerAveraging.Value)
            {
                Utils.LogError("Unable to return player, meter updates aborted");
                return;
            }

            //
            // good to update meter
            //
            updateTimer += Time.deltaTime;
            if (updateTimer > 1f / SamplesPerSec.Value)
            {
                updateTimer = 0;
                UpdateLightMeter();
            }
            //CameraRig.UpdateDebugLines();
        }

        public void CleanupRaid()
        {
            _player = null;
            _playerRendererCache.Clear();
            _asyncReadbackPending = false;
            _asyncScoreReady = false;
            IsRaid = false;
        }

        public void StartRaid()
        {
            _player = Utils.GetMainPlayer();
            SetupRenderTexture();
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

            if (UseAsyncGPUReadback.Value && SystemInfo.supportsAsyncGPUReadback)
            {
                ConsumeAsyncLightMeterScore();
                TryScheduleAsyncLightMeterScore(playersList);
                return;
            }

            float score;
            if (!TryGetLightMeterScore(playersList, out score))
            {
                return;
            }

            debugScore = score;
            RecalcMeterAverage(score);

            
        }

        void ConsumeAsyncLightMeterScore()
        {
            if (!_asyncScoreReady)
            {
                return;
            }

            _asyncScoreReady = false;
            debugScore = _asyncScore;
            RecalcMeterAverage(_asyncScore);
        }

        void TryScheduleAsyncLightMeterScore(List<Player> playersList)
        {
            if (_asyncReadbackPending)
            {
                return;
            }

            if (UseFikaPlayerAveraging.Value)
            {
                ScheduleAsyncFikaAveragedLightMeterScore(playersList);
                return;
            }

            Player player = Utils.GetMainPlayer();
            if (!CameraRig.TryRepositionCamera(player, playersList))
            {
                return;
            }

            AsyncScoreBatch batch = CreateAsyncScoreBatch(1);
            RenderAndRequestScoreReadback(player, batch);
        }

        void ScheduleAsyncFikaAveragedLightMeterScore(List<Player> playersList)
        {
            List<Player> targetPlayers = Utils.GetActualHumanPlayers(playersList);
            List<Player> botPlayers = Utils.GetBotPlayers(playersList);

            if (targetPlayers.Count == 0 || botPlayers.Count == 0)
            {
                return;
            }

            List<Player> validTargets = new List<Player>();
            foreach (Player targetPlayer in targetPlayers)
            {
                if (CameraRig.TryRepositionCamera(targetPlayer, botPlayers))
                {
                    validTargets.Add(targetPlayer);
                }
            }

            if (validTargets.Count == 0)
            {
                return;
            }

            AsyncScoreBatch batch = CreateAsyncScoreBatch(validTargets.Count);
            foreach (Player targetPlayer in validTargets)
            {
                CameraRig.TryRepositionCamera(targetPlayer, botPlayers);
                RenderAndRequestScoreReadback(targetPlayer, batch);
            }
        }

        bool TryGetLightMeterScore(List<Player> playersList, out float score)
        {
            if (UseFikaPlayerAveraging.Value)
            {
                return TryGetFikaAveragedLightMeterScore(playersList, out score);
            }

            score = 0f;
            Player player = Utils.GetMainPlayer();
            if (!CameraRig.TryRepositionCamera(player, playersList))
            {
                return false;
            }

            score = RenderAndDispatchShader(player);
            return true;
        }

        bool TryGetFikaAveragedLightMeterScore(List<Player> playersList, out float score)
        {
            score = 0f;
            List<Player> targetPlayers = Utils.GetActualHumanPlayers(playersList);
            List<Player> botPlayers = Utils.GetBotPlayers(playersList);

            if (targetPlayers.Count == 0 || botPlayers.Count == 0)
            {
                return false;
            }

            float scoreSum = 0f;
            int scoreCount = 0;

            foreach (Player targetPlayer in targetPlayers)
            {
                if (!CameraRig.TryRepositionCamera(targetPlayer, botPlayers))
                {
                    continue;
                }

                scoreSum += RenderAndDispatchShader(targetPlayer);
                scoreCount++;
            }

            if (scoreCount == 0)
            {
                return false;
            }

            score = scoreSum / scoreCount;
            return true;
        }

        void RecalcMeterAverage(float meterThisFrame)
        {
            _lightMeterPool -= _avgLightMeter;
            _lightMeterPool += meterThisFrame;
            _lightMeterPool = Mathf.Clamp(_lightMeterPool, 0.01f, 10f);
            if (float.IsNaN(_lightMeterPool)) _lightMeterPool = 1f;

            _avgLightMeter = _lightMeterPool * Time.deltaTime * 50f;
            _avgLightMeter = Mathf.Clamp(_avgLightMeter, 0.01f, 1f);
            if (float.IsNaN(_avgLightMeter)) _avgLightMeter = 1f;
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
            int configuredTexSize = GetConfiguredTextureSize();
            if (_rt != null && outputBuffer != null && configuredTexSize == _texSize)
            {
                ApplyLightCameraSettings(0);
                return;
            }

            ReleaseRenderResources();
            _texSize = configuredTexSize;

            _rt = new RenderTexture(_texSize, _texSize, 16, RenderTextureFormat.ARGB32);
            _rt.enableRandomWrite = false;
            _rt.depth = 16;
            _rt.stencilFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.None;
            _rt.dimension = TextureDimension.Tex2D;
            _rt.Create();

            if (_lightCam == null)
            {
                _lightCam = gameObject.AddComponent<Camera>();
            }

            _lightCam.targetTexture = _rt;
            _lightCam.enabled = false;
            ApplyLightCameraSettings(0);
            CameraRig.Initialize(_lightCam);

            // Prepare output buffer
            outputColors = new Color[_texSize * _texSize];
            outputBuffer = new ComputeBuffer(outputColors.Length, sizeof(float) * 4);

            // Set kernel handle for compute shader
            _handleMain = _computeShader.FindKernel("CSMain");

            _computeShader.SetTexture(_handleMain, "textureInput", _rt);
            _computeShader.SetBuffer(_handleMain, "outputBuffer", outputBuffer);
        }

        int GetConfiguredTextureSize()
        {
            int configuredValue = Mathf.RoundToInt(RenderTextureResolution.Value);
            configuredValue = Mathf.Clamp(configuredValue, 16, 512);
            return Mathf.Max(8, Mathf.RoundToInt(configuredValue / 8f) * 8);
        }

        void ReleaseRenderResources()
        {
            _asyncReadbackPending = false;
            _asyncScoreReady = false;
            _asyncBatchId++;

            if (outputBuffer != null)
            {
                outputBuffer.Release();
                outputBuffer = null;
            }

            if (_rt != null)
            {
                _rt.Release();
                Destroy(_rt);
                _rt = null;
            }
        }

        int GetPlayerLayerMask()
        {
            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer < 0)
            {
                return 0;
            }

            return 1 << playerLayer;
        }

        int GetRendererLayerMask(Renderer[] renderers)
        {
            int mask = 0;
            if (renderers == null)
            {
                return mask;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                mask |= 1 << renderer.gameObject.layer;
            }

            return mask;
        }

        int GetLightCameraCullingMask(Renderer[] targetRenderers)
        {
            if (!RenderPlayerOnly.Value)
            {
                return -1;
            }

            int playerLayerMask = GetPlayerLayerMask();
            if (ForceTargetPlayerRenderers.Value && playerLayerMask != 0)
            {
                return playerLayerMask;
            }

            int rendererLayerMask = GetRendererLayerMask(targetRenderers);
            if (rendererLayerMask != 0)
            {
                return rendererLayerMask;
            }

            return playerLayerMask != 0 ? playerLayerMask : -1;
        }

        void ApplyLightCameraSettings(int targetPlayerCullingMask)
        {
            if (_lightCam == null)
            {
                return;
            }

            int effectiveCullingMask = targetPlayerCullingMask != 0 ? targetPlayerCullingMask : GetLightCameraCullingMask(null);
            _lightCam.clearFlags = RenderPlayerOnly.Value ? CameraClearFlags.Color : CameraClearFlags.Skybox;
            _lightCam.backgroundColor = Color.black;
            _lightCam.cullingMask = effectiveCullingMask;
            _lightCam.renderingPath = RenderPlayerOnly.Value ? RenderingPath.Forward : RenderingPath.DeferredShading;
            _lightCam.allowHDR = false;
            _lightCam.allowMSAA = false;
            _lightCam.useOcclusionCulling = false;
            _lightCam.nearClipPlane = 0.01f;
            _lightCam.farClipPlane = RenderPlayerOnly.Value ? 20f : 200f;
        }

        Color[] outputColors;

        float DispatchShader()
        {
            _computeShader.Dispatch(_handleMain, _texSize / 8, _texSize / 8, 1);
            outputBuffer.GetData(outputColors);

            float result = GetBreadth(outputColors);
            if (UseLuma.Value)
            {
                result += GetLuma(outputColors);
                result /= 2f;
            }
            return result;
        }

        float RenderAndDispatchShader(Player targetPlayer)
        {
            Renderer[] targetRenderers = GetCachedPlayerRenderers(targetPlayer);
            ApplyLightCameraSettings(GetLightCameraCullingMask(targetRenderers));
            using (new TargetPlayerRenderScope(targetRenderers, ForceTargetPlayerRenderers.Value, RenderPlayerOnly.Value))
            {
                _lightCam.Render();
            }
            return DispatchShader();
        }

        AsyncScoreBatch CreateAsyncScoreBatch(int requestCount)
        {
            _asyncReadbackPending = true;
            _asyncBatchId++;
            return new AsyncScoreBatch
            {
                Id = _asyncBatchId,
                Pending = requestCount,
                Settings = CaptureScoreSettings()
            };
        }

        void RenderAndRequestScoreReadback(Player targetPlayer, AsyncScoreBatch batch)
        {
            Renderer[] targetRenderers = GetCachedPlayerRenderers(targetPlayer);
            ApplyLightCameraSettings(GetLightCameraCullingMask(targetRenderers));
            using (new TargetPlayerRenderScope(targetRenderers, ForceTargetPlayerRenderers.Value, RenderPlayerOnly.Value))
            {
                _lightCam.Render();
            }

            AsyncGPUReadback.Request(_rt, 0, TextureFormat.RGBA32, request => HandleAsyncScoreReadback(request, batch));
        }

        void HandleAsyncScoreReadback(AsyncGPUReadbackRequest request, AsyncScoreBatch batch)
        {
            if (_isDestroyed || batch.Id != _asyncBatchId)
            {
                return;
            }

            if (!request.hasError && TryCalculateScore(request, batch.Settings, out float score))
            {
                batch.ScoreSum += score;
                batch.ScoreCount++;
            }

            batch.Pending--;
            if (batch.Pending > 0)
            {
                return;
            }

            _asyncReadbackPending = false;
            if (batch.ScoreCount == 0)
            {
                return;
            }

            _asyncScore = batch.ScoreSum / batch.ScoreCount;
            _asyncScoreReady = true;
        }

        ScoreSettings CaptureScoreSettings()
        {
            return new ScoreSettings
            {
                UseLuma = UseLuma.Value,
                LumaCoef = LumaCoef.Value,
                RedLumaMulti = RedLumaMulti.Value,
                GreenLumaMulti = GreenLumaMulti.Value,
                BlueLumaMulti = BlueLumaMulti.Value,
                RedBreadthMulti = RedBreadthMulti.Value,
                GreenBreadthMulti = GreenBreadthMulti.Value,
                BlueBreadthMulti = BlueBreadthMulti.Value
            };
        }

        bool TryCalculateScore(AsyncGPUReadbackRequest request, ScoreSettings settings, out float score)
        {
            score = 0f;
            var pixels = request.GetData<Color32>();
            int pixelCount = pixels.Length;
            if (pixelCount == 0)
            {
                return false;
            }

            int rLow = 255;
            int gLow = 255;
            int bLow = 255;
            int rHigh = 0;
            int gHigh = 0;
            int bHigh = 0;

            float rLumaSum = 0f;
            float gLumaSum = 0f;
            float bLumaSum = 0f;

            for (int i = 0; i < pixelCount; i++)
            {
                Color32 pixel = pixels[i];

                if (pixel.r < rLow) rLow = pixel.r;
                if (pixel.g < gLow) gLow = pixel.g;
                if (pixel.b < bLow) bLow = pixel.b;

                if (pixel.r > rHigh) rHigh = pixel.r;
                if (pixel.g > gHigh) gHigh = pixel.g;
                if (pixel.b > bHigh) bHigh = pixel.b;

                if (settings.UseLuma)
                {
                    rLumaSum += pixel.r * settings.RedLumaMulti;
                    gLumaSum += pixel.g * settings.BlueLumaMulti;
                    bLumaSum += pixel.b * settings.GreenLumaMulti;
                }
            }

            float breadth = ((rHigh - rLow) * settings.RedBreadthMulti + (gHigh - gLow) * settings.GreenBreadthMulti + (bHigh - bLow) * settings.BlueBreadthMulti) / (255f * 3f);
            debugScore2 = breadth;
            score = breadth;

            if (settings.UseLuma)
            {
                float luma = (rLumaSum + gLumaSum + bLumaSum) / (255f * pixelCount);
                luma *= settings.LumaCoef;
                score += luma;
                score /= 2f;
            }

            return !float.IsNaN(score) && !float.IsInfinity(score);
        }

        Renderer[] GetCachedPlayerRenderers(Player targetPlayer)
        {
            if (targetPlayer == null)
            {
                return Array.Empty<Renderer>();
            }

            if (_playerRendererCache.TryGetValue(targetPlayer, out Renderer[] renderers) && renderers != null && !HasNullRenderer(renderers))
            {
                return renderers;
            }

            renderers = targetPlayer.GetComponentsInChildren<Renderer>(true);
            _playerRendererCache[targetPlayer] = renderers;
            return renderers;
        }

        bool HasNullRenderer(Renderer[] renderers)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                {
                    return true;
                }
            }

            return false;
        }

        class AsyncScoreBatch
        {
            public int Id;
            public int Pending;
            public int ScoreCount;
            public float ScoreSum;
            public ScoreSettings Settings;
        }

        struct ScoreSettings
        {
            public bool UseLuma;
            public float LumaCoef;
            public float RedLumaMulti;
            public float GreenLumaMulti;
            public float BlueLumaMulti;
            public float RedBreadthMulti;
            public float GreenBreadthMulti;
            public float BlueBreadthMulti;
        }

        int _handleMain;
        public uint[] _histogramData;


        float _lightMeterPool = 0f;
        float _avgLightMeter = 0.01f;
        public float FinalLightMeter = 0.01f;


        float _finalValueLerped = 0.01f;

        void ClampFinalValue()
        {
            float finalValue = Mathf.Clamp(_avgLightMeter, 0.01f, 1f);
            //_finalValueLerped = Mathf.Lerp(_finalValueLerped, finalValue, Time.deltaTime * 20f);
            _finalValueLerped = finalValue;
            if (float.IsNaN(_finalValueLerped)) _finalValueLerped = 1f;

            float meterCoef = 1f - MeterAttenuationCoef.Value;
            FinalLightMeter = Mathf.Lerp(_finalValueLerped, 1f, meterCoef);
            Utils.Log($"_finalValueBeforeMod : {_finalValueLerped} // final light output : {FinalLightMeter}", false);
        }

        GUIStyle efficiencyIndicatorStyle = new GUIStyle();

        void OnGUI()
        {
            if (!MasterSwitch.Value)
            { 
                return; 
            }
            if (Utils.IsInRaid())
            {
                if (MeterViz.Value)
                {
                    efficiencyIndicatorStyle.normal.textColor = Color.grey;
                    efficiencyIndicatorStyle.fontSize = 20;
                    float indicatorHorizontalPos = 20f;
                    float indicatorVerticalPos = 10f;
                    string input = Visualiser.GetLevelString(_finalValueLerped, false);
                    GUI.Label(new Rect(indicatorHorizontalPos, indicatorVerticalPos, 40f, 40f), input, efficiencyIndicatorStyle);
                }

                if (IsDebug.Value)
                {
                    if (MeterViz.Value)
                    {
                        string debugString = string.Format($"luma is {debugScore}, breadth is {debugScore2}");
                        GUI.Label(new Rect(20f, 50f, 40f, 40f), debugString, efficiencyIndicatorStyle);
                    }
                }

                DrawRenderTexturePreview();
            }
        }

        void DrawRenderTexturePreview()
        {
            if (!ShowRenderTexturePreview.Value || _rt == null)
            {
                return;
            }

            float previewSize = RenderTexturePreviewSize.Value;
            Rect previewRect = new Rect(20f, 80f, previewSize, previewSize);
            Rect frameRect = new Rect(previewRect.x - 2f, previewRect.y - 2f, previewRect.width + 4f, previewRect.height + 4f);

            Color previousColor = GUI.color;
            GUI.color = Color.black;
            GUI.DrawTexture(frameRect, Texture2D.whiteTexture, ScaleMode.StretchToFill);
            GUI.color = Color.white;
            GUI.DrawTexture(previewRect, _rt, ScaleMode.ScaleToFit, false);
            GUI.color = previousColor;
        }

        sealed class TargetPlayerRenderScope : IDisposable
        {
            struct RendererState
            {
                public Renderer Renderer;
                public bool Enabled;
                public bool ForceRenderingOff;
                public ShadowCastingMode ShadowCastingMode;
                public int Layer;
                public bool HasSkinnedMeshRenderer;
                public bool UpdateWhenOffscreen;
            }

            readonly List<RendererState> _rendererStates = new List<RendererState>();
            bool _disposed;

            public TargetPlayerRenderScope(Renderer[] renderers, bool forceRenderers, bool forcePlayerLayer)
            {
                if (!forceRenderers || renderers == null || renderers.Length == 0)
                {
                    return;
                }

                int playerLayer = LayerMask.NameToLayer("Player");
                bool canForcePlayerLayer = forcePlayerLayer && playerLayer >= 0;

                foreach (Renderer renderer in renderers)
                {
                    if (renderer == null)
                    {
                        continue;
                    }

                    SkinnedMeshRenderer skinnedMeshRenderer = renderer as SkinnedMeshRenderer;
                    _rendererStates.Add(new RendererState
                    {
                        Renderer = renderer,
                        Enabled = renderer.enabled,
                        ForceRenderingOff = renderer.forceRenderingOff,
                        ShadowCastingMode = renderer.shadowCastingMode,
                        Layer = renderer.gameObject.layer,
                        HasSkinnedMeshRenderer = skinnedMeshRenderer != null,
                        UpdateWhenOffscreen = skinnedMeshRenderer != null && skinnedMeshRenderer.updateWhenOffscreen
                    });

                    renderer.enabled = true;
                    renderer.forceRenderingOff = false;
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    if (skinnedMeshRenderer != null)
                    {
                        skinnedMeshRenderer.updateWhenOffscreen = true;
                    }

                    if (canForcePlayerLayer)
                    {
                        renderer.gameObject.layer = playerLayer;
                    }
                }
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                for (int i = _rendererStates.Count - 1; i >= 0; i--)
                {
                    RendererState state = _rendererStates[i];
                    if (state.Renderer == null)
                    {
                        continue;
                    }

                    state.Renderer.enabled = state.Enabled;
                    state.Renderer.forceRenderingOff = state.ForceRenderingOff;
                    state.Renderer.shadowCastingMode = state.ShadowCastingMode;
                    state.Renderer.gameObject.layer = state.Layer;
                    if (state.HasSkinnedMeshRenderer)
                    {
                        ((SkinnedMeshRenderer)state.Renderer).updateWhenOffscreen = state.UpdateWhenOffscreen;
                    }
                }

                _disposed = true;
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
            return luma * LumaCoef.Value;
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

        List<Player> _botsToEvaluate = new List<Player>();

        void UpdateBotsToEvaluate()
        {
            _botsToEvaluate = Utils.GetAllPlayers();
            foreach (var bot in _botsToEvaluate)
            {
                Player player = Utils.GetMainPlayer();
                if (bot == player)
                {
                    _botsToEvaluate.Remove(bot);
                    continue;
                }

                Vector3 raycastVector = player.PlayerBones.Head.position - bot.PlayerBones.Head.position;
                if (!Physics.Raycast(bot.PlayerBones.Head.position, raycastVector, out RaycastHit hit, 200f))
                {
                    _botsToEvaluate.Remove(bot);
                }

                float hitDist = hit.distance;
            }
        }

    }
}
