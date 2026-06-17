using BepInEx;
using BepInEx.Configuration;
using EFT;
using SPT.Reflection.Patching;
using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
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
        readonly Queue<float> _meterSamples = new Queue<float>();
        float _meterSampleSum;

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
        public static ConfigEntry<bool> UseFikaPlayerAveraging;

        // settings
        public static ConfigEntry<float> MeterAttenuationCoef;
        public static ConfigEntry<float> SamplesPerSec;
        public static ConfigEntry<float> MeterAverageSamples;
        public static ConfigEntry<float> AimNerf;

        // adv settings
        public static ConfigEntry<float> CameraFOV;
        public static ConfigEntry<float> LumaCoef;
        public static ConfigEntry<float> RenderTextureResolution;
        public static ConfigEntry<bool> UseAsyncGPUReadback;
        public static ConfigEntry<bool> IgnoreTransparentPixels;

        // color settings
        public static ConfigEntry<float> RedLumaMulti;
        public static ConfigEntry<float> GreenLumaMulti;
        public static ConfigEntry<float> BlueLumaMulti;
        public static ConfigEntry<float> RedColorDepthMulti;
        public static ConfigEntry<float> GreenColorDepthMulti;
        public static ConfigEntry<float> BlueColorDepthMulti;

        // camera rig settings
        public static ConfigEntry<float> CamHorizontalOffset;
        public static ConfigEntry<float> CameraFocusHeightOffset;
        public static ConfigEntry<bool> ForceTargetPlayerRenderers;
        public static ConfigEntry<bool> UseOrbitCameraSampling;
        public static ConfigEntry<float> OrbitCameraRadius;
        public static ConfigEntry<float> OrbitCameraHeightOffset;
        public static ConfigEntry<bool> RejectOccludedSamples;
        public static ConfigEntry<bool> ExcludeOpticRenderers;

        // debug values
        public static ConfigEntry<float> DebugUpdateFreq;
        public static ConfigEntry<bool> IsDebug;
        public static ConfigEntry<bool> ShowRenderTexturePreview;
        public static ConfigEntry<float> RenderTexturePreviewSize;
        public static ConfigEntry<bool> UseFixedOrbitAngle;
        public static ConfigEntry<float> FixedOrbitAngle;

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
            UseFikaPlayerAveraging = ConstructBoolConfig(false, "a - Toggles", "Use Fika player averaging", "When enabled, target all real non-headless Fika client players and average each player's visibility from their nearest bot. Safe to leave disabled when Fika is not installed.");

            // main settings
            SamplesPerSec = ConstructFloatConfig(1f, "b - Main Settings", "1-Light samples per second", "Main throttle of the mod; higher = more accurate reading / less perf", 1f, 60f);
            MeterAverageSamples = ConstructFloatConfig(15f, "b - Main Settings", "2-Light meter average samples", "Number of valid light samples to average before applying the result. Higher values smooth noisy orbit sampling.", 1f, 600f);
            MeterAttenuationCoef = ConstructFloatConfig(1f, "b - Main Settings", "3-Light meter strength", "Determines how quickly bots can spot you per your visiblity level (100% = bots get full effect, slower recognition time)", 0f, 1f);
            AimNerf = ConstructFloatConfig(0.03f, "b - Main Settings", "4-Bot aim handicap", "Determines how much bots' aim is affected by your visibility level (higher = bots' aim more nerfed by your viz level; zero = effect is removed", 0f, 0.1f);

            // adv settings
            CameraFOV = ConstructFloatConfig(30f, "c - Advanced Settings", "CameraFOV", "Size of light camera FOV", 10f, 170f);
            LumaCoef = ConstructFloatConfig(6f, "c - Advanced Settings", "Luma coefficient", "Multiplies the luma result", 1f, 20f);
            RenderTextureResolution = ConstructFloatConfig(64f, "c - Advanced Settings", "Render texture resolution", "Resolution of the light camera render texture. Applied before raid start and rounded to the nearest multiple of 8.", 16f, 512f);
            UseAsyncGPUReadback = ConstructBoolConfig(true, "c - Advanced Settings", "Use async GPU readback", "Avoids blocking the main thread while reading the light camera texture. Disable to use the old synchronous compute readback path.");
            IgnoreTransparentPixels = ConstructBoolConfig(true, "c - Advanced Settings", "Ignore transparent pixels", "Ignores transparent clear/background pixels when calculating luma and color depth");

            // color multis
            // traditional luma values : r 0.2126729, g 0.7151522, b 0.0721750
            RedLumaMulti = ConstructFloatConfig(0.79f, "d - Color Settings", "1-Red luma multi", "Red color in pixel analysis is multiplied by this to produce the luma calculation", 0f, 1f);
            GreenLumaMulti = ConstructFloatConfig(0.29f, "d - Color Settings", "2-Green luma multi", "Green color in pixel analysis is multiplied by this to produce the luma calculation", 0f, 1f);
            BlueLumaMulti = ConstructFloatConfig(0.93f, "d - Color Settings", "3-Blue luma multi", "Blue color in pixel analysis is multiplied by this to produce the luma calculation", 0f, 1f);
            RedColorDepthMulti = ConstructFloatConfig(0.79f, "d - Color Settings", "4-Red color depth multi", "Red color range in pixel analysis is multiplied by this to produce the color depth calculation", 0f, 1f);
            GreenColorDepthMulti = ConstructFloatConfig(0.29f, "d - Color Settings", "5-Green color depth multi", "Green color range in pixel analysis is multiplied by this to produce the color depth calculation", 0f, 1f);
            BlueColorDepthMulti = ConstructFloatConfig(0.93f, "d - Color Settings", "6-Blue color depth multi", "Blue color range in pixel analysis is multiplied by this to produce the color depth calculation", 0f, 1f);

            // camera rig
            CamHorizontalOffset = ConstructFloatConfig(4f, "e - Camera Rig Settings", "Camera horizontal offset", "Distance between the camera and the player focus point on horizontal plane", 0.1f, 5f);
            CameraFocusHeightOffset = ConstructFloatConfig(-0.2f, "e - Camera Rig Settings", "Camera focus height offset", "Vertical offset from the player's ribcage bone. Negative values focus lower on the chest.", -1f, 1f);
            ForceTargetPlayerRenderers = ConstructBoolConfig(true, "e - Camera Rig Settings", "Force target player renderers", "Temporarily forces the sampled player's renderers visible and renderable by the light camera, then restores them");
            UseOrbitCameraSampling = ConstructBoolConfig(true, "e - Camera Rig Settings", "Use orbit camera sampling", "Samples real human players from a random orbit around the chest instead of sampling from the nearest bot position");
            OrbitCameraRadius = ConstructFloatConfig(4f, "e - Camera Rig Settings", "Orbit camera radius", "Distance from the player's chest when orbit camera sampling is enabled", 0.25f, 12f);
            OrbitCameraHeightOffset = ConstructFloatConfig(1.5f, "e - Camera Rig Settings", "Orbit camera height offset", "Vertical offset above the player's chest when orbit camera sampling is enabled", -1f, 4f);
            RejectOccludedSamples = ConstructBoolConfig(true, "e - Camera Rig Settings", "Reject occluded samples", "Skips a light camera sample when world geometry blocks the ray from the light camera to the player's chest");
            ExcludeOpticRenderers = ConstructBoolConfig(true, "e - Camera Rig Settings", "Exclude optic renderers", "Hides ranged optic item renderers while the light camera renders");

            // debug
            IsDebug = ConstructBoolConfig(false, "y - Debug", "1) Enable debug logging", "");
            DebugUpdateFreq = ConstructFloatConfig(1f, "y - Debug", "2) Debug updates per second", "How frequently the debug logger updates per second", 1f, 10f);
            ShowRenderTexturePreview = ConstructBoolConfig(false, "y - Debug", "3) Show render texture preview", "Draws the light-meter render texture in the game window for debugging");
            RenderTexturePreviewSize = ConstructFloatConfig(256f, "y - Debug", "4) Render texture preview size", "Size of the render texture debug preview in pixels", 64f, 512f);
            UseFixedOrbitAngle = ConstructBoolConfig(false, "y - Debug", "5) Use fixed orbit angle", "Uses the configured orbit angle instead of a random orbit angle for the actual light-meter sample");
            FixedOrbitAngle = ConstructFloatConfig(0f, "y - Debug", "6) Fixed orbit angle", "Camera angle around the sampled player's chest when fixed orbit sampling is enabled", 0f, 360f);

            RemoveObsoleteConfigEntries();
        }

        void RemoveObsoleteConfigEntries()
        {
            bool removed = false;
            removed |= RemoveObsoleteConfigEntry("a - Toggles", "Use Luma meter");
            removed |= RemoveObsoleteConfigEntry("c - Advanced Settings", "Analysis exposure multiplier");
            removed |= RemoveObsoleteConfigEntry("c - Advanced Settings", "Player render fill intensity");
            removed |= RemoveObsoleteConfigEntry("c - Advanced Settings", "Environment render fill intensity");
            removed |= RemoveObsoleteConfigEntry("c - Advanced Settings", "Use light camera fill light");
            removed |= RemoveObsoleteConfigEntry("c - Advanced Settings", "Light camera fill intensity");
            removed |= RemoveObsoleteConfigEntry("c - Advanced Settings", "Exclude sky from render texture");
            removed |= RemoveObsoleteConfigEntry("e - Camera Rig Settings", "Render player only");
            removed |= RemoveObsoleteConfigEntry("e - Camera Rig Settings", "Exclude in-hands item renderers");
            removed |= RemoveObsoleteConfigEntry("d - Color Settings", "4-Red breadth multi");
            removed |= RemoveObsoleteConfigEntry("d - Color Settings", "5-Green breadth multi");
            removed |= RemoveObsoleteConfigEntry("d - Color Settings", "6-Blue breadth multi");
            removed |= RemoveObsoleteConfigEntry("z - Dev", "dev1");
            removed |= RemoveObsoleteConfigEntry("z - Dev", "dev2");

            if (removed)
            {
                Config.Save();
            }
        }

        bool RemoveObsoleteConfigEntry(string section, string key)
        {
            ConfigDefinition definition = new ConfigDefinition(section, key);
            if (!Config.ContainsKey(definition))
            {
                return false;
            }

            Config.Remove(definition);
            return true;
        }

        float updateTimer = 0f;

        void Update()
        {
            if (!MasterSwitch.Value)
            {
                return;
            }
            PluginManager.Update();

            if (!IsRaid)
            {
                return;
            }
            if (_player == null)
            {
                _player = Utils.GetMainPlayer();
            }
            if (_player == null && !UseFikaPlayerAveraging.Value && !UseOrbitCameraSampling.Value)
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
            ResetMeterAverage();
            _asyncReadbackPending = false;
            _asyncScoreReady = false;
            IsRaid = false;
        }

        public void StartRaid()
        {
            _player = Utils.GetMainPlayer();
            ResetMeterAverage();
            SetupRenderTexture();
            IsRaid = true;
        }

        float debugScore = 0f;
        float debugLumaScore = 0f;
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

            List<Player> targetPlayers = GetSampleTargetPlayers(playersList);
            List<Player> observerPlayers = GetSampleObserverPlayers(playersList);
            if (targetPlayers.Count == 0)
            {
                return;
            }

            AsyncScoreBatch batch = null;
            foreach (Player targetPlayer in targetPlayers)
            {
                if (!TryPrepareLightCameraSample(targetPlayer, observerPlayers))
                {
                    continue;
                }

                if (batch == null)
                {
                    batch = CreateAsyncScoreBatch();
                }

                batch.Pending++;
                RenderAndRequestScoreReadback(targetPlayer, batch);
            }
        }

        bool TryGetLightMeterScore(List<Player> playersList, out float score)
        {
            score = 0f;
            List<Player> targetPlayers = GetSampleTargetPlayers(playersList);
            List<Player> observerPlayers = GetSampleObserverPlayers(playersList);
            if (targetPlayers.Count == 0)
            {
                return false;
            }

            float scoreSum = 0f;
            int scoreCount = 0;

            foreach (Player targetPlayer in targetPlayers)
            {
                if (!TryPrepareLightCameraSample(targetPlayer, observerPlayers))
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

        List<Player> GetSampleTargetPlayers(List<Player> playersList)
        {
            if (UseOrbitCameraSampling.Value || UseFikaPlayerAveraging.Value)
            {
                List<Player> humanPlayers = Utils.GetActualHumanPlayers(playersList);
                if (humanPlayers.Count > 0)
                {
                    return humanPlayers;
                }
            }

            List<Player> result = new List<Player>();
            Player player = Utils.GetMainPlayer();
            if (Utils.IsLightMeterUsablePlayer(player))
            {
                result.Add(player);
            }

            return result;
        }

        List<Player> GetSampleObserverPlayers(List<Player> playersList)
        {
            if (UseOrbitCameraSampling.Value)
            {
                return null;
            }

            return UseFikaPlayerAveraging.Value ? Utils.GetBotPlayers(playersList) : playersList;
        }

        bool TryPrepareLightCameraSample(Player targetPlayer, List<Player> observerPlayers)
        {
            Vector3 focusPoint;
            bool positioned = UseOrbitCameraSampling.Value
                ? CameraRig.TryRepositionCameraOnOrbit(targetPlayer, OrbitCameraRadius.Value, OrbitCameraHeightOffset.Value, GetSamplingOrbitAngle(), out focusPoint)
                : CameraRig.TryRepositionCamera(targetPlayer, observerPlayers, out focusPoint);

            if (!positioned)
            {
                return false;
            }

            return !RejectOccludedSamples.Value || HasLineOfSightToFocus(focusPoint);
        }

        float GetSamplingOrbitAngle()
        {
            if (UseFixedOrbitAngle.Value)
            {
                return FixedOrbitAngle.Value;
            }

            return UnityEngine.Random.Range(0f, 360f);
        }

        bool HasLineOfSightToFocus(Vector3 focusPoint)
        {
            if (_lightCam == null)
            {
                return false;
            }

            Vector3 cameraPosition = _lightCam.transform.position;
            Vector3 direction = focusPoint - cameraPosition;
            float distance = direction.magnitude;
            if (distance <= 0.001f || float.IsNaN(distance) || float.IsInfinity(distance))
            {
                return false;
            }

            RaycastHit hit;
            return !Physics.Raycast(cameraPosition, direction / distance, out hit, distance, GetSampleOcclusionMask(), QueryTriggerInteraction.Ignore);
        }

        int GetSampleOcclusionMask()
        {
            int mask = LayerMask.GetMask("Terrain", "HighPolyCollider", "LowPolyCollider", "DoorLowPolyCollider");
            return mask != 0 ? mask : Physics.DefaultRaycastLayers & ~GetPlayerLayerMask();
        }

        void RecalcMeterAverage(float meterThisFrame)
        {
            if (float.IsNaN(meterThisFrame) || float.IsInfinity(meterThisFrame))
            {
                return;
            }

            int maxSamples = Mathf.Clamp(Mathf.RoundToInt(MeterAverageSamples.Value), 1, 600);
            _meterSamples.Enqueue(meterThisFrame);
            _meterSampleSum += meterThisFrame;

            while (_meterSamples.Count > maxSamples)
            {
                _meterSampleSum -= _meterSamples.Dequeue();
            }

            _avgLightMeter = _meterSamples.Count > 0 ? _meterSampleSum / _meterSamples.Count : meterThisFrame;
            _avgLightMeter = Mathf.Clamp(_avgLightMeter, 0.01f, 1f);
            if (float.IsNaN(_avgLightMeter)) _avgLightMeter = 1f;
            ClampFinalValue();
        }

        void ResetMeterAverage()
        {
            _meterSamples.Clear();
            _meterSampleSum = 0f;
            _avgLightMeter = 0.01f;
            _finalValueLerped = 0.01f;
            FinalLightMeter = 0.01f;
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
                ApplyLightCameraSettings(_lightCam);
                return;
            }

            ReleaseRenderResources();
            _texSize = configuredTexSize;

            _rt = CreateLightMeterRenderTexture();

            if (_lightCam == null)
            {
                _lightCam = gameObject.AddComponent<Camera>();
            }

            _lightCam.targetTexture = _rt;
            _lightCam.enabled = false;
            ApplyLightCameraSettings(_lightCam);
            CameraRig.Initialize(_lightCam);

            // Prepare output buffer
            outputColors = new Color[_texSize * _texSize];
            outputBuffer = new ComputeBuffer(outputColors.Length, sizeof(float) * 4);

            // Set kernel handle for compute shader
            _handleMain = _computeShader.FindKernel("CSMain");

            _computeShader.SetBuffer(_handleMain, "outputBuffer", outputBuffer);
        }

        RenderTexture CreateLightMeterRenderTexture()
        {
            RenderTexture renderTexture = new RenderTexture(_texSize, _texSize, 16, RenderTextureFormat.ARGB32);
            renderTexture.enableRandomWrite = false;
            renderTexture.depth = 16;
            renderTexture.stencilFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.None;
            renderTexture.dimension = TextureDimension.Tex2D;
            renderTexture.Create();
            return renderTexture;
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

        void ApplyLightCameraSettings(Camera lightCamera)
        {
            if (lightCamera == null)
            {
                return;
            }

            lightCamera.clearFlags = CameraClearFlags.Skybox;
            lightCamera.backgroundColor = Color.black;
            lightCamera.cullingMask = -1;
            lightCamera.renderingPath = RenderingPath.UsePlayerSettings;
            lightCamera.allowHDR = false;
            lightCamera.allowMSAA = false;
            lightCamera.useOcclusionCulling = true;
            lightCamera.nearClipPlane = 0.01f;
            lightCamera.farClipPlane = 20f;
        }

        Color[] outputColors;

        bool TryDispatchRenderTexture(RenderTexture sourceTexture, ScoreSettings settings, out RenderStats stats)
        {
            stats = null;
            if (sourceTexture == null)
            {
                return false;
            }

            _computeShader.SetTexture(_handleMain, "textureInput", sourceTexture);
            _computeShader.Dispatch(_handleMain, _texSize / 8, _texSize / 8, 1);
            outputBuffer.GetData(outputColors);
            return TryCalculateRenderStats(outputColors, settings, out stats);
        }

        float RenderAndDispatchShader(Player targetPlayer)
        {
            SampleRendererSet rendererSet = GetSampleRendererSet(targetPlayer);
            RenderLightCamera(rendererSet);
            ScoreSettings settings = CaptureScoreSettings();

            if (!TryDispatchRenderTexture(_rt, settings, out RenderStats stats))
            {
                return 0.01f;
            }

            return CombineRenderStats(stats, settings);
        }

        AsyncScoreBatch CreateAsyncScoreBatch()
        {
            _asyncReadbackPending = true;
            _asyncBatchId++;
            return new AsyncScoreBatch
            {
                Id = _asyncBatchId,
                Pending = 0,
                Settings = CaptureScoreSettings()
            };
        }

        void RenderAndRequestScoreReadback(Player targetPlayer, AsyncScoreBatch batch)
        {
            SampleRendererSet rendererSet = GetSampleRendererSet(targetPlayer);
            RenderLightCamera(rendererSet);
            AsyncGPUReadback.Request(_rt, 0, TextureFormat.RGBA32, request => HandleAsyncScoreReadback(request, batch));
        }

        void RenderLightCamera(SampleRendererSet rendererSet)
        {
            ApplyLightCameraSettings(_lightCam);
            RenderTexture previousTargetTexture = _lightCam.targetTexture;
            _lightCam.targetTexture = _rt;
            try
            {
                using (new TargetPlayerRenderScope(rendererSet.VisibleRenderers, rendererSet.HiddenRenderers, ForceTargetPlayerRenderers.Value))
                {
                    _lightCam.Render();
                }
            }
            finally
            {
                _lightCam.targetTexture = previousTargetTexture;
            }
        }

        void HandleAsyncScoreReadback(AsyncGPUReadbackRequest request, AsyncScoreBatch batch)
        {
            if (_isDestroyed || batch.Id != _asyncBatchId)
            {
                return;
            }

            RenderStats stats = null;
            if (!request.hasError && TryCalculateRenderStats(request.GetData<Color32>(), batch.Settings, out stats))
            {
                batch.ScoreSum += CombineRenderStats(stats, batch.Settings);
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
                LumaCoef = LumaCoef.Value,
                RedLumaMulti = RedLumaMulti.Value,
                GreenLumaMulti = GreenLumaMulti.Value,
                BlueLumaMulti = BlueLumaMulti.Value,
                RedColorDepthMulti = RedColorDepthMulti.Value,
                GreenColorDepthMulti = GreenColorDepthMulti.Value,
                BlueColorDepthMulti = BlueColorDepthMulti.Value,
                IgnoreTransparentPixels = IgnoreTransparentPixels.Value
            };
        }

        bool TryCalculateRenderStats(NativeArray<Color32> pixels, ScoreSettings settings, out RenderStats stats)
        {
            stats = new RenderStats();
            int pixelCount = pixels.Length;
            if (pixelCount == 0)
            {
                return false;
            }

            for (int i = 0; i < pixelCount; i++)
            {
                Color32 pixel = pixels[i];
                if (ShouldIgnorePixel(pixel.a / 255f, settings.IgnoreTransparentPixels))
                {
                    continue;
                }

                AccumulateRenderStats(stats, pixel.r, pixel.g, pixel.b, settings);
            }

            return FinalizeRenderStats(stats, settings);
        }

        bool TryCalculateRenderStats(Color[] pixels, ScoreSettings settings, out RenderStats stats)
        {
            stats = new RenderStats();
            if (pixels == null || pixels.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < pixels.Length; i++)
            {
                Color pixel = pixels[i];
                if (ShouldIgnorePixel(pixel.a, settings.IgnoreTransparentPixels))
                {
                    continue;
                }

                AccumulateRenderStats(stats, pixel.r * 255f, pixel.g * 255f, pixel.b * 255f, settings);
            }

            return FinalizeRenderStats(stats, settings);
        }

        bool ShouldIgnorePixel(float alpha, bool ignoreTransparent)
        {
            return ignoreTransparent && alpha <= 0.001f;
        }

        void AccumulateRenderStats(RenderStats stats, float r, float g, float b, ScoreSettings settings)
        {
            stats.SampleCount++;
            stats.RLumaSum += r * settings.RedLumaMulti;
            stats.GLumaSum += g * settings.GreenLumaMulti;
            stats.BLumaSum += b * settings.BlueLumaMulti;

            if (r < stats.RLow) stats.RLow = r;
            if (g < stats.GLow) stats.GLow = g;
            if (b < stats.BLow) stats.BLow = b;

            if (r > stats.RHigh) stats.RHigh = r;
            if (g > stats.GHigh) stats.GHigh = g;
            if (b > stats.BHigh) stats.BHigh = b;
        }

        bool FinalizeRenderStats(RenderStats stats, ScoreSettings settings)
        {
            if (stats.SampleCount == 0)
            {
                return false;
            }

            stats.Luma = (stats.RLumaSum + stats.GLumaSum + stats.BLumaSum) / (255f * stats.SampleCount);
            stats.ColorDepth = ((stats.RHigh - stats.RLow) * settings.RedColorDepthMulti + (stats.GHigh - stats.GLow) * settings.GreenColorDepthMulti + (stats.BHigh - stats.BLow) * settings.BlueColorDepthMulti) / (255f * 3f);
            return !float.IsNaN(stats.Luma) && !float.IsInfinity(stats.Luma);
        }

        float CombineRenderStats(RenderStats stats, ScoreSettings settings)
        {
            float lumaScore = Mathf.Clamp01(stats.Luma * settings.LumaCoef);
            float colorDepth = Mathf.Clamp01(stats.ColorDepth);

            debugLumaScore = lumaScore;
            debugScore2 = colorDepth;

            float score = (lumaScore + colorDepth) * 0.5f;
            return Mathf.Clamp(score, 0.01f, 1f);
        }

        SampleRendererSet GetSampleRendererSet(Player targetPlayer)
        {
            Renderer[] renderers = GetCachedPlayerRenderers(targetPlayer);
            if (renderers.Length == 0 || !ExcludeOpticRenderers.Value)
            {
                return new SampleRendererSet(renderers, Array.Empty<Renderer>());
            }

            List<Renderer> visibleRenderers = new List<Renderer>(renderers.Length);
            List<Renderer> hiddenRenderers = new List<Renderer>();
            GameObject handsObject = GetHandsControllerObject(targetPlayer);
            HashSet<Renderer> rangedOpticRenderers = GetRangedOpticRendererSet(handsObject);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (ShouldHideSampleRenderer(renderer, rangedOpticRenderers))
                {
                    hiddenRenderers.Add(renderer);
                    continue;
                }

                visibleRenderers.Add(renderer);
            }

            return new SampleRendererSet(visibleRenderers.ToArray(), hiddenRenderers.ToArray());
        }

        GameObject GetHandsControllerObject(Player targetPlayer)
        {
            if (targetPlayer == null || targetPlayer.HandsController == null)
            {
                return null;
            }

            return targetPlayer.HandsController.ControllerGameObject;
        }

        HashSet<Renderer> GetRangedOpticRendererSet(GameObject handsObject)
        {
            HashSet<Renderer> result = new HashSet<Renderer>();
            if (handsObject == null)
            {
                return result;
            }

            SightModVisualControllers[] sightControllers = handsObject.GetComponentsInChildren<SightModVisualControllers>(true);
            for (int i = 0; i < sightControllers.Length; i++)
            {
                SightModVisualControllers sightController = sightControllers[i];
                if (!IsRangedOpticVisual(sightController))
                {
                    continue;
                }

                Renderer[] opticRenderers = sightController.GetComponentsInChildren<Renderer>(true);
                for (int rendererIndex = 0; rendererIndex < opticRenderers.Length; rendererIndex++)
                {
                    Renderer opticRenderer = opticRenderers[rendererIndex];
                    if (opticRenderer != null)
                    {
                        result.Add(opticRenderer);
                    }
                }
            }

            return result;
        }

        bool IsRangedOpticVisual(SightModVisualControllers sightController)
        {
            if (sightController == null || sightController.SightMod == null)
            {
                return false;
            }

            if (sightController.TryGetZoomHandler(out ScopeZoomHandler zoomHandler) && zoomHandler != null)
            {
                return true;
            }

            ScopePrefabCache scopePrefabCache = sightController.GetComponent<ScopePrefabCache>();
            return scopePrefabCache != null && scopePrefabCache.HasOptics;
        }

        bool ShouldHideSampleRenderer(Renderer renderer, HashSet<Renderer> rangedOpticRenderers)
        {
            if (!ExcludeOpticRenderers.Value || renderer == null)
            {
                return false;
            }

            if (rangedOpticRenderers != null && rangedOpticRenderers.Contains(renderer))
            {
                return true;
            }

            return RendererLooksLikeOptic(renderer);
        }

        bool RendererLooksLikeOptic(Renderer renderer)
        {
            if (renderer == null)
            {
                return false;
            }

            Transform current = renderer.transform;
            for (int depth = 0; current != null && depth < 8; depth++)
            {
                if (NameContainsOpticContextToken(current.name))
                {
                    return true;
                }

                current = current.parent;
            }

            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null)
                {
                    continue;
                }

                if (NameContainsOpticContextToken(material.name) || NameContainsOpticContextToken(material.shader != null ? material.shader.name : null))
                {
                    return true;
                }

            }

            return false;
        }

        bool NameContainsOpticContextToken(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            value = value.ToLowerInvariant();
            return value.Contains("optic")
                || value.Contains("scope")
                || value.Contains("magnifier")
                || value.Contains("prism")
                || value.Contains("acog")
                || value.Contains("elcan")
                || value.Contains("hamr")
                || value.Contains("bravo")
                || value.Contains("valday")
                || value.Contains("razor")
                || value.Contains("vudu")
                || value.Contains("tac30")
                || value.Contains("march")
                || value.Contains("nightforce")
                || value.Contains("schmidt")
                || value.Contains("leupold");
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

        struct SampleRendererSet
        {
            public readonly Renderer[] VisibleRenderers;
            public readonly Renderer[] HiddenRenderers;

            public SampleRendererSet(Renderer[] visibleRenderers, Renderer[] hiddenRenderers)
            {
                VisibleRenderers = visibleRenderers ?? Array.Empty<Renderer>();
                HiddenRenderers = hiddenRenderers ?? Array.Empty<Renderer>();
            }
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
            public float LumaCoef;
            public float RedLumaMulti;
            public float GreenLumaMulti;
            public float BlueLumaMulti;
            public float RedColorDepthMulti;
            public float GreenColorDepthMulti;
            public float BlueColorDepthMulti;
            public bool IgnoreTransparentPixels;
        }

        class RenderStats
        {
            public int SampleCount;
            public float RLow = 255f;
            public float GLow = 255f;
            public float BLow = 255f;
            public float RHigh;
            public float GHigh;
            public float BHigh;
            public float RLumaSum;
            public float GLumaSum;
            public float BLumaSum;
            public float Luma;
            public float ColorDepth;
        }

        int _handleMain;


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
                        string debugString = string.Format($"score {debugScore}, luma {debugLumaScore}, color depth {debugScore2}");
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
                public bool HasSkinnedMeshRenderer;
                public bool UpdateWhenOffscreen;
            }

            readonly List<RendererState> _rendererStates = new List<RendererState>();
            bool _disposed;

            public TargetPlayerRenderScope(Renderer[] visibleRenderers, Renderer[] hiddenRenderers, bool forceRenderers)
            {
                HideRenderers(hiddenRenderers);

                if (!forceRenderers || visibleRenderers == null || visibleRenderers.Length == 0)
                {
                    return;
                }

                foreach (Renderer renderer in visibleRenderers)
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
                }
            }

            void HideRenderers(Renderer[] renderers)
            {
                if (renderers == null || renderers.Length == 0)
                {
                    return;
                }

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
                        HasSkinnedMeshRenderer = skinnedMeshRenderer != null,
                        UpdateWhenOffscreen = skinnedMeshRenderer != null && skinnedMeshRenderer.updateWhenOffscreen
                    });

                    renderer.forceRenderingOff = true;
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
                    if (state.HasSkinnedMeshRenderer)
                    {
                        ((SkinnedMeshRenderer)state.Renderer).updateWhenOffscreen = state.UpdateWhenOffscreen;
                    }
                }

                _disposed = true;
            }
        }

    }
}
