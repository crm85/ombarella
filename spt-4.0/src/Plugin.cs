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
        const string modVersion = "0.5.1";
        const string ConfigSectionGeneral = "a - General";
        const string ConfigSectionCamera = "b - Camera";
        const string ConfigSectionLuma = "c - Luma";
        const string ConfigSectionColor = "d - Color";
        const string ConfigSectionBlend = "e - Luma + Color Blend";
        const string ConfigSectionDebug = "f - Debug";

        Player _player;
        Camera _lightCam;
        Light _colorRenderFillLight;
        RenderTexture _rt;
        RenderTexture _colorRt;
        int _texSize = 128;
        bool _isDestroyed;
        bool _asyncReadbackPending;
        bool _asyncScoreReady;
        bool _hasValidMeterSample;
        bool _syncFallbackWarningLogged;
        float _asyncScore = 0.01f;
        int _asyncBatchId;
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
            DestroyColorRenderFillLight();
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

        // color settings
        public static ConfigEntry<float> RedLumaMulti;
        public static ConfigEntry<float> GreenLumaMulti;
        public static ConfigEntry<float> BlueLumaMulti;
        public static ConfigEntry<float> RedColorBreadthMulti;
        public static ConfigEntry<float> GreenColorBreadthMulti;
        public static ConfigEntry<float> BlueColorBreadthMulti;
        public static ConfigEntry<float> ColorRenderFillIntensity;
        public static ConfigEntry<float> LumaColorDistribution;

        // camera rig settings
        public static ConfigEntry<float> CameraFocusHeightOffset;
        public static ConfigEntry<bool> UseOrbitCameraSampling;
        public static ConfigEntry<float> OrbitCameraRadius;
        public static ConfigEntry<float> OrbitCameraHeightOffset;
        public static ConfigEntry<bool> RejectOccludedSamples;

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
            LoadConfig();
            LoadPatches();
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
            // general
            MasterSwitch = ConstructBoolConfig(true, ConfigSectionGeneral, "1-Master switch", "Toggle all mod functions on/off", OldConfig("a - Toggles", "Master Switch"));
            MeterViz = ConstructBoolConfig(true, ConfigSectionGeneral, "2-Enable light meter indicator", "Visual representation of how much you are being lit and how visible you are", OldConfig("a - Toggles", "Enable light meter indicator"));
            UseFikaPlayerAveraging = ConstructBoolConfig(false, ConfigSectionGeneral, "3-Use Fika player averaging", "When enabled, target all real non-headless Fika client players and average each player's visibility from their nearest bot. Safe to leave disabled when Fika is not installed.", OldConfig("a - Toggles", "Use Fika player averaging"));
            SamplesPerSec = ConstructFloatConfig(60f, ConfigSectionGeneral, "4-Light samples per second", "Main throttle of the mod; higher = more accurate reading / less perf", 1f, 60f, OldConfig("b - Main Settings", "1-Light samples per second"));
            MeterAverageSamples = ConstructFloatConfig(15f, ConfigSectionGeneral, "5-Light meter average samples", "Number of valid light samples to average before applying the result. Higher values smooth noisy orbit sampling.", 1f, 600f, OldConfig("b - Main Settings", "2-Light meter average samples"));
            MeterAttenuationCoef = ConstructFloatConfig(1f, ConfigSectionGeneral, "6-Light meter strength", "Determines how quickly bots can spot you per your visiblity level (100% = bots get full effect, slower recognition time)", 0f, 1f, OldConfig("b - Main Settings", "3-Light meter strength"));
            AimNerf = ConstructFloatConfig(0.03f, ConfigSectionGeneral, "7-Bot aim handicap", "Determines how much bots' aim is affected by your visibility level (higher = bots' aim more nerfed by your viz level; zero = effect is removed", 0f, 0.1f, OldConfig("b - Main Settings", "4-Bot aim handicap"));
            UseAsyncGPUReadback = ConstructBoolConfig(true, ConfigSectionGeneral, "8-Use async GPU readback", "Avoids blocking the main thread while reading the light camera texture. Disable to use the old synchronous compute readback path.", OldConfig("c - Advanced Settings", "Use async GPU readback"));

            // camera
            CameraFOV = ConstructFloatConfig(30f, ConfigSectionCamera, "1-Camera FOV", "Size of light camera FOV", 10f, 170f, OldConfig("c - Advanced Settings", "CameraFOV"));
            RenderTextureResolution = ConstructFloatConfig(128f, ConfigSectionCamera, "2-Render texture resolution", "Resolution of each light camera render texture. Applied before raid start and rounded to the nearest multiple of 8.", 16f, 512f, OldConfig("c - Advanced Settings", "Render texture resolution"));
            CameraFocusHeightOffset = ConstructFloatConfig(-0.2f, ConfigSectionCamera, "3-Camera focus height offset", "Vertical offset from the player's ribcage bone. Negative values focus lower on the chest.", -1f, 1f, OldConfig("e - Camera Rig Settings", "Camera focus height offset"));
            UseOrbitCameraSampling = ConstructBoolConfig(true, ConfigSectionCamera, "4-Use orbit camera sampling", "Samples real human players from a random orbit around the chest instead of sampling from the nearest bot position", OldConfig("e - Camera Rig Settings", "Use orbit camera sampling"), OldConfig(ConfigSectionCamera, "5-Use orbit camera sampling"));
            OrbitCameraRadius = ConstructFloatConfig(4f, ConfigSectionCamera, "5-Orbit camera radius", "Distance from the player's chest when orbit camera sampling is enabled", 0.25f, 12f, OldConfig("e - Camera Rig Settings", "Orbit camera radius"), OldConfig(ConfigSectionCamera, "6-Orbit camera radius"));
            OrbitCameraHeightOffset = ConstructFloatConfig(1.5f, ConfigSectionCamera, "6-Orbit camera height offset", "Vertical offset above the player's chest when orbit camera sampling is enabled", -1f, 4f, OldConfig("e - Camera Rig Settings", "Orbit camera height offset"), OldConfig(ConfigSectionCamera, "7-Orbit camera height offset"));
            RejectOccludedSamples = ConstructBoolConfig(true, ConfigSectionCamera, "7-Reject occluded samples", "Skips a light camera sample when world geometry blocks the ray from the light camera to the player's chest", OldConfig("e - Camera Rig Settings", "Reject occluded samples"), OldConfig(ConfigSectionCamera, "8-Reject occluded samples"));

            // luma
            // traditional luma values : r 0.2126729, g 0.7151522, b 0.0721750
            LumaCoef = ConstructFloatConfig(15f, ConfigSectionLuma, "1-Luma coefficient", "Multiplies the luma result", 1f, 20f, OldConfig("c - Advanced Settings", "Luma coefficient"));
            RedLumaMulti = ConstructFloatConfig(1f, ConfigSectionLuma, "2-Red luma multi", "Red color in pixel analysis is multiplied by this to produce the luma calculation", 0f, 1f, OldConfig("d - Color Settings", "1-Red luma multi"));
            GreenLumaMulti = ConstructFloatConfig(1f, ConfigSectionLuma, "3-Green luma multi", "Green color in pixel analysis is multiplied by this to produce the luma calculation", 0f, 1f, OldConfig("d - Color Settings", "2-Green luma multi"));
            BlueLumaMulti = ConstructFloatConfig(1f, ConfigSectionLuma, "4-Blue luma multi", "Blue color in pixel analysis is multiplied by this to produce the luma calculation", 0f, 1f, OldConfig("d - Color Settings", "3-Blue luma multi"));

            // color
            RedColorBreadthMulti = ConstructFloatConfig(1f, ConfigSectionColor, "1-Red color breadth multi", "Red channel range in the color render is multiplied by this for the color breadth score", 0f, 1f, OldConfig("d - Color Settings", "4-Red color depth multi"), OldConfig("d - Color Settings", "4-Red breadth multi"));
            GreenColorBreadthMulti = ConstructFloatConfig(0.4f, ConfigSectionColor, "2-Green color breadth multi", "Green channel range in the color render is multiplied by this for the color breadth score", 0f, 1f, OldConfig("d - Color Settings", "5-Green color depth multi"), OldConfig("d - Color Settings", "5-Green breadth multi"));
            BlueColorBreadthMulti = ConstructFloatConfig(1f, ConfigSectionColor, "3-Blue color breadth multi", "Blue channel range in the color render is multiplied by this for the color breadth score", 0f, 1f, OldConfig("d - Color Settings", "6-Blue color depth multi"), OldConfig("d - Color Settings", "6-Blue breadth multi"));
            ColorRenderFillIntensity = ConstructFloatConfig(0.5f, ConfigSectionColor, "4-Color render fill intensity", "Temporary camera-aligned fill light intensity used only for the color-breadth render. Luma rendering is not filled.", 0f, 8f, OldConfig(ConfigSectionColor, "1-Player profile fill intensity"), OldConfig(ConfigSectionColor, "2-Environment profile fill intensity"), OldConfig("c - Advanced Settings", "Player profile fill intensity"), OldConfig("c - Advanced Settings", "Environment profile fill intensity"));

            // blend
            LumaColorDistribution = ConstructFloatConfig(GetMigratedLumaColorDistribution(0.4f), ConfigSectionBlend, "1-Luma color distribution", "Normalized final score distribution. Zero is luma only; one is color breadth only.", 0f, 1f);

            // debug
            IsDebug = ConstructBoolConfig(false, ConfigSectionDebug, "1-Enable debug logging", "", OldConfig("y - Debug", "1) Enable debug logging"));
            DebugUpdateFreq = ConstructFloatConfig(1f, ConfigSectionDebug, "2-Debug updates per second", "How frequently the debug logger updates per second", 1f, 10f, OldConfig("y - Debug", "2) Debug updates per second"));
            ShowRenderTexturePreview = ConstructBoolConfig(false, ConfigSectionDebug, "3-Show render texture preview", "Draws the light-meter render texture in the game window for debugging", OldConfig("y - Debug", "3) Show render texture preview"));
            RenderTexturePreviewSize = ConstructFloatConfig(256f, ConfigSectionDebug, "4-Render texture preview size", "Size of the render texture debug preview in pixels", 64f, 512f, OldConfig("y - Debug", "4) Render texture preview size"));
            UseFixedOrbitAngle = ConstructBoolConfig(false, ConfigSectionDebug, "5-Use fixed orbit angle", "Uses the configured orbit angle instead of a random orbit angle for the actual light-meter sample", OldConfig("y - Debug", "5) Use fixed orbit angle"));
            FixedOrbitAngle = ConstructFloatConfig(0f, ConfigSectionDebug, "6-Fixed orbit angle", "Camera angle around the sampled player's chest when fixed orbit sampling is enabled", 0f, 360f, OldConfig("y - Debug", "6) Fixed orbit angle"));

            RemoveObsoleteConfigEntries();
        }

        void RemoveObsoleteConfigEntries()
        {
            bool removed = false;
            removed |= RemoveObsoleteConfigEntries("a - Toggles",
                "Master Switch",
                "Enable light meter indicator",
                "Use Fika player averaging",
                "Use Luma meter");
            removed |= RemoveObsoleteConfigEntries("b - Main Settings",
                "1-Light samples per second",
                "2-Light meter average samples",
                "3-Light meter strength",
                "4-Bot aim handicap");
            removed |= RemoveObsoleteConfigEntries("c - Advanced Settings",
                "CameraFOV",
                "Luma coefficient",
                "Render texture resolution",
                "Player profile fill intensity",
                "Environment profile fill intensity",
                "Use async GPU readback",
                "Ignore transparent pixels",
                "Analysis exposure multiplier",
                "Player render fill intensity",
                "Environment render fill intensity",
                "Use light camera fill light",
                "Light camera fill intensity",
                "Exclude sky from render texture");
            removed |= RemoveObsoleteConfigEntries("d - Color Settings",
                "1-Red luma multi",
                "2-Green luma multi",
                "3-Blue luma multi",
                "4-Color profile minimum visibility",
                "5-Color profile mismatch power",
                "6-Color profile influence",
                "4-Red breadth multi",
                "5-Green breadth multi",
                "6-Blue breadth multi",
                "4-Red color depth multi",
                "5-Green color depth multi",
                "6-Blue color depth multi");
            removed |= RemoveObsoleteConfigEntries(ConfigSectionColor,
                "1-Player profile fill intensity",
                "2-Environment profile fill intensity",
                "3-Ignore transparent pixels");
            removed |= RemoveObsoleteConfigEntries(ConfigSectionBlend,
                "1-Color profile minimum visibility",
                "2-Color profile mismatch power",
                "3-Color profile influence",
                "1-Luma blend weight",
                "2-Color breadth blend weight");
            removed |= RemoveObsoleteConfigEntries("e - Camera Rig Settings",
                "Camera horizontal offset",
                "Camera focus height offset",
                "Force target player renderers",
                "Use orbit camera sampling",
                "Orbit camera radius",
                "Orbit camera height offset",
                "Reject occluded samples",
                "Exclude optic renderers",
                "Render player only",
                "Exclude in-hands item renderers");
            removed |= RemoveObsoleteConfigEntries(ConfigSectionCamera,
                "4-Camera horizontal offset",
                "5-Use orbit camera sampling",
                "6-Orbit camera radius",
                "7-Orbit camera height offset",
                "8-Reject occluded samples",
                "9-Force target player renderers",
                "10-Exclude optic renderers");
            removed |= RemoveObsoleteConfigEntries("y - Debug",
                "1) Enable debug logging",
                "2) Debug updates per second",
                "3) Show render texture preview",
                "4) Render texture preview size",
                "5) Use fixed orbit angle",
                "6) Fixed orbit angle");
            removed |= RemoveObsoleteConfigEntry("z - Dev", "dev1");
            removed |= RemoveObsoleteConfigEntry("z - Dev", "dev2");

            if (removed)
            {
                Config.Save();
            }
        }

        bool RemoveObsoleteConfigEntries(string section, params string[] keys)
        {
            bool removed = false;
            if (keys == null)
            {
                return false;
            }

            for (int i = 0; i < keys.Length; i++)
            {
                removed |= RemoveObsoleteConfigEntry(section, keys[i]);
            }

            return removed;
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
            if (MasterSwitch == null || !MasterSwitch.Value)
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
            if (_lightCam == null)
            {
                SetupRenderTexture();
            }

            if (_lightCam == null)
            {
                return;
            }

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

                RenderAndRequestScoreReadback(targetPlayer, batch);
            }
        }

        bool TryGetLightMeterScore(List<Player> playersList, out float score)
        {
            score = 0f;
            if (!_computeShaderReady)
            {
                if (!_syncFallbackWarningLogged)
                {
                    _syncFallbackWarningLogged = true;
                    Logger.LogWarning("Ombarella synchronous light meter path is disabled because the compute shader is unavailable.");
                }

                return false;
            }

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

                if (!TryRenderAndDispatchShader(targetPlayer, out float sampleScore))
                {
                    continue;
                }

                scoreSum += sampleScore;
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
            _hasValidMeterSample = true;
            ClampFinalValue();
        }

        void ResetMeterAverage()
        {
            _meterSamples.Clear();
            _meterSampleSum = 0f;
            _hasValidMeterSample = false;
            _avgLightMeter = 1f;
            _finalValueLerped = 1f;
            FinalLightMeter = 1f;
        }

        ConfigEntry<float> ConstructFloatConfig(float defaultValue, string category, string descriptionShort, string descriptionFull, float min, float max, params ConfigDefinition[] migrateFrom)
        {
            float migratedDefaultValue = GetMigratedConfigValue(defaultValue, migrateFrom);
            ConfigEntry<float> result = ((BaseUnityPlugin)this).Config.Bind<float>(category, descriptionShort, migratedDefaultValue, new ConfigDescription(descriptionFull, (AcceptableValueBase)(object)new AcceptableValueRange<float>(min, max), Array.Empty<object>()));
            return result;
        }

        ConfigEntry<bool> ConstructBoolConfig(bool defaultValue, string category, string descriptionShort, string descriptionFull, params ConfigDefinition[] migrateFrom)
        {
            bool migratedDefaultValue = GetMigratedConfigValue(defaultValue, migrateFrom);
            ConfigEntry<bool> result = ((BaseUnityPlugin)this).Config.Bind<bool>(category, descriptionShort, migratedDefaultValue, new ConfigDescription(descriptionFull, (AcceptableValueBase)null, Array.Empty<object>()));
            return result;
        }

        ConfigDefinition OldConfig(string section, string key)
        {
            return new ConfigDefinition(section, key);
        }

        T GetMigratedConfigValue<T>(T defaultValue, ConfigDefinition[] migrateFrom)
        {
            if (migrateFrom == null)
            {
                return defaultValue;
            }

            for (int i = 0; i < migrateFrom.Length; i++)
            {
                ConfigDefinition definition = migrateFrom[i];
                if (definition == null || !Config.ContainsKey(definition))
                {
                    continue;
                }

                object oldValue = Config[definition].BoxedValue;
                if (oldValue is T typedValue)
                {
                    return typedValue;
                }

                try
                {
                    return (T)Convert.ChangeType(oldValue, typeof(T));
                }
                catch
                {
                    return defaultValue;
                }
            }

            return defaultValue;
        }

        float GetMigratedLumaColorDistribution(float defaultValue)
        {
            if (!TryGetConfigFloat(OldConfig(ConfigSectionBlend, "1-Luma blend weight"), out float lumaWeight)
                || !TryGetConfigFloat(OldConfig(ConfigSectionBlend, "2-Color breadth blend weight"), out float colorWeight))
            {
                return defaultValue;
            }

            lumaWeight = Mathf.Max(0f, lumaWeight);
            colorWeight = Mathf.Max(0f, colorWeight);
            float totalWeight = lumaWeight + colorWeight;
            if (totalWeight <= 0.001f)
            {
                return defaultValue;
            }

            return Mathf.Clamp01(colorWeight / totalWeight);
        }

        bool TryGetConfigFloat(ConfigDefinition definition, out float value)
        {
            value = 0f;
            if (definition == null || !Config.ContainsKey(definition))
            {
                return false;
            }

            object rawValue = Config[definition].BoxedValue;
            if (rawValue is float floatValue)
            {
                value = floatValue;
                return true;
            }

            try
            {
                value = Convert.ToSingle(rawValue);
                return true;
            }
            catch
            {
                return false;
            }
        }

        ComputeShader _computeShader;
        bool _computeShaderReady;

        void PopulateShader()
        {
            _computeShader = null;
            _computeShaderReady = false;

            AssetBundle bundle = LoadComputeShaderBundle();
            if (bundle == null)
            {
                Logger.LogWarning("Ombarella light meter compute shader bundle was not found. Async GPU readback can still work, but synchronous fallback is disabled.");
                return;
            }

            try
            {
                _computeShader = bundle.LoadAsset<ComputeShader>("GetAllPixelColors");
                _computeShaderReady = _computeShader != null;
                string isNull = _computeShaderReady ? "is loaded" : "is NULL";
                Debug.Log($"shader {isNull}");
            }
            finally
            {
                bundle.Unload(false);
            }
        }

        AssetBundle LoadComputeShaderBundle()
        {
            string[] candidatePaths = new[]
            {
                Path.Combine(BepInEx.Paths.PluginPath, "Ombarella", "shader"),
                Path.Combine(BepInEx.Paths.PluginPath, "Ombarella", "ombhistogram")
            };

            for (int i = 0; i < candidatePaths.Length; i++)
            {
                string candidatePath = candidatePaths[i];
                if (!File.Exists(candidatePath))
                {
                    continue;
                }

                AssetBundle bundle = AssetBundle.LoadFromFile(candidatePath);
                if (bundle != null)
                {
                    Logger.LogInfo($"Loaded light meter shader bundle: {candidatePath}");
                    return bundle;
                }

                Logger.LogWarning($"Failed to load light meter shader bundle: {candidatePath}");
            }

            return null;
        }

        private ComputeBuffer outputBuffer;

        void SetupRenderTexture()
        {
            int configuredTexSize = GetConfiguredTextureSize();
            if (_rt != null && _colorRt != null && (!_computeShaderReady || outputBuffer != null) && configuredTexSize == _texSize)
            {
                ApplyLightCameraSettings(_lightCam);
                return;
            }

            ReleaseRenderResources();
            _texSize = configuredTexSize;

            _rt = CreateLightMeterRenderTexture();
            _colorRt = CreateLightMeterRenderTexture();

            if (_lightCam == null)
            {
                _lightCam = gameObject.AddComponent<Camera>();
            }

            _lightCam.targetTexture = _rt;
            _lightCam.enabled = false;
            ApplyLightCameraSettings(_lightCam);
            CameraRig.Initialize(_lightCam);

            if (_computeShaderReady)
            {
                // Prepare output buffer
                outputColors = new Color[_texSize * _texSize];
                outputBuffer = new ComputeBuffer(outputColors.Length, sizeof(float) * 4);

                // Set kernel handle for compute shader
                _handleMain = _computeShader.FindKernel("CSMain");

                _computeShader.SetBuffer(_handleMain, "outputBuffer", outputBuffer);
            }
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

            if (_colorRt != null)
            {
                _colorRt.Release();
                Destroy(_colorRt);
                _colorRt = null;
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

        bool TryDispatchLumaStats(RenderTexture sourceTexture, ScoreSettings settings, out RenderStats stats)
        {
            stats = null;
            if (!TryDispatchRenderTexture(sourceTexture))
            {
                return false;
            }

            return TryCalculateLumaStats(outputColors, settings, out stats);
        }

        bool TryDispatchColorBreadthStats(RenderTexture sourceTexture, ScoreSettings settings, out RenderStats stats)
        {
            stats = null;
            if (!TryDispatchRenderTexture(sourceTexture))
            {
                return false;
            }

            return TryCalculateColorBreadthStats(outputColors, settings, out stats);
        }

        bool TryDispatchRenderTexture(RenderTexture sourceTexture)
        {
            if (sourceTexture == null || !_computeShaderReady || outputBuffer == null)
            {
                return false;
            }

            _computeShader.SetTexture(_handleMain, "textureInput", sourceTexture);
            _computeShader.Dispatch(_handleMain, _texSize / 8, _texSize / 8, 1);
            outputBuffer.GetData(outputColors);
            return true;
        }

        bool TryRenderAndDispatchShader(Player targetPlayer, out float score)
        {
            score = 0f;
            ScoreSettings settings = CaptureScoreSettings();

            RenderLightCamera(_rt, false, targetPlayer);
            if (!TryDispatchLumaStats(_rt, settings, out RenderStats lumaStats))
            {
                return false;
            }

            RenderLightCamera(_colorRt, true, targetPlayer);
            if (!TryDispatchColorBreadthStats(_colorRt, settings, out RenderStats colorStats))
            {
                return false;
            }

            score = CombineTwoPassScore(lumaStats, colorStats, settings);
            return true;
        }

        public bool CanApplyBotVisibilityPatch()
        {
            return MasterSwitch != null && MasterSwitch.Value && IsRaid && _hasValidMeterSample;
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
            AsyncScoreSample sample = new AsyncScoreSample
            {
                Batch = batch,
                Pending = 2
            };

            batch.Pending += sample.Pending;

            RenderLightCamera(_rt, false, targetPlayer);
            AsyncGPUReadback.Request(_rt, 0, TextureFormat.RGBA32, request => HandleAsyncScoreReadback(request, sample, LightMeterRenderPass.Luma));

            RenderLightCamera(_colorRt, true, targetPlayer);
            AsyncGPUReadback.Request(_colorRt, 0, TextureFormat.RGBA32, request => HandleAsyncScoreReadback(request, sample, LightMeterRenderPass.ColorBreadth));
        }

        void RenderLightCamera(RenderTexture targetTexture, bool enableColorFill, Player targetPlayer)
        {
            ApplyLightCameraSettings(_lightCam);
            if (!enableColorFill)
            {
                DisableColorRenderFillLight();
            }

            RenderTexture previousTargetTexture = _lightCam.targetTexture;
            _lightCam.targetTexture = targetTexture;
            bool fillLightEnabled = enableColorFill && EnableColorRenderFillLight(_lightCam);
            MagnifiedOpticRenderScope magnifiedOpticRenderScope = CreateMagnifiedOpticRenderScope(targetPlayer);
            FirstPersonBodyRenderScope firstPersonBodyRenderScope = CreateFirstPersonBodyRenderScope(targetPlayer, magnifiedOpticRenderScope != null ? magnifiedOpticRenderScope.HiddenRenderers : null);

            try
            {
                _lightCam.Render();
            }
            finally
            {
                if (firstPersonBodyRenderScope != null)
                {
                    firstPersonBodyRenderScope.Dispose();
                }

                if (magnifiedOpticRenderScope != null)
                {
                    magnifiedOpticRenderScope.Dispose();
                }

                if (fillLightEnabled)
                {
                    DisableColorRenderFillLight();
                }

                _lightCam.targetTexture = previousTargetTexture;
            }
        }

        MagnifiedOpticRenderScope CreateMagnifiedOpticRenderScope(Player targetPlayer)
        {
            if (!Utils.IsLightMeterUsablePlayer(targetPlayer))
            {
                return null;
            }

            GameObject handsObject = GetHandsControllerObject(targetPlayer);
            if (handsObject == null)
            {
                return null;
            }

            HashSet<Renderer> opticRenderers = GetMagnifiedOpticRenderers(handsObject);
            return opticRenderers.Count > 0 ? new MagnifiedOpticRenderScope(opticRenderers) : null;
        }

        GameObject GetHandsControllerObject(Player targetPlayer)
        {
            if (targetPlayer == null || targetPlayer.HandsController == null)
            {
                return null;
            }

            return targetPlayer.HandsController.ControllerGameObject;
        }

        HashSet<Renderer> GetMagnifiedOpticRenderers(GameObject handsObject)
        {
            HashSet<Renderer> opticRenderers = new HashSet<Renderer>();
            if (handsObject == null)
            {
                return opticRenderers;
            }

            SightModVisualControllers[] sightControllers = handsObject.GetComponentsInChildren<SightModVisualControllers>(true);
            for (int i = 0; i < sightControllers.Length; i++)
            {
                SightModVisualControllers sightController = sightControllers[i];
                if (!IsMagnifiedOpticVisual(sightController))
                {
                    continue;
                }

                AddRenderers(sightController.gameObject, opticRenderers);
            }

            return opticRenderers;
        }

        bool IsMagnifiedOpticVisual(SightModVisualControllers sightController)
        {
            if (sightController == null)
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

        void AddRenderers(GameObject rootObject, HashSet<Renderer> renderers)
        {
            if (rootObject == null || renderers == null)
            {
                return;
            }

            Renderer[] foundRenderers = rootObject.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < foundRenderers.Length; i++)
            {
                Renderer renderer = foundRenderers[i];
                if (renderer != null)
                {
                    renderers.Add(renderer);
                }
            }
        }

        FirstPersonBodyRenderScope CreateFirstPersonBodyRenderScope(Player targetPlayer, HashSet<Renderer> excludedRenderers)
        {
            if (!Utils.IsLightMeterUsablePlayer(targetPlayer))
            {
                return null;
            }

            List<Renderer> renderers = new List<Renderer>();
            HashSet<Renderer> seenRenderers = new HashSet<Renderer>();
            AddShadowsOnlyRenderers(targetPlayer.gameObject, renderers, seenRenderers, excludedRenderers);
            if (targetPlayer.HandsController != null)
            {
                AddShadowsOnlyRenderers(targetPlayer.HandsController.ControllerGameObject, renderers, seenRenderers, excludedRenderers);
            }

            return renderers.Count > 0 ? new FirstPersonBodyRenderScope(renderers) : null;
        }

        void AddShadowsOnlyRenderers(GameObject rootObject, List<Renderer> renderers, HashSet<Renderer> seenRenderers, HashSet<Renderer> excludedRenderers)
        {
            if (rootObject == null)
            {
                return;
            }

            Renderer[] foundRenderers = rootObject.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < foundRenderers.Length; i++)
            {
                Renderer renderer = foundRenderers[i];
                if (renderer != null && !IsExcludedRenderer(renderer, excludedRenderers) && renderer.shadowCastingMode == ShadowCastingMode.ShadowsOnly && seenRenderers.Add(renderer))
                {
                    renderers.Add(renderer);
                }
            }
        }

        bool IsExcludedRenderer(Renderer renderer, HashSet<Renderer> excludedRenderers)
        {
            return renderer != null && excludedRenderers != null && excludedRenderers.Contains(renderer);
        }

        bool EnableColorRenderFillLight(Camera lightCamera)
        {
            if (lightCamera == null || ColorRenderFillIntensity.Value <= 0f)
            {
                return false;
            }

            if (!EnsureColorRenderFillLight())
            {
                return false;
            }

            _colorRenderFillLight.type = LightType.Directional;
            _colorRenderFillLight.color = Color.white;
            _colorRenderFillLight.intensity = ColorRenderFillIntensity.Value;
            _colorRenderFillLight.bounceIntensity = 0f;
            _colorRenderFillLight.shadows = LightShadows.None;
            _colorRenderFillLight.renderMode = LightRenderMode.ForcePixel;
            _colorRenderFillLight.cullingMask = -1;
            _colorRenderFillLight.transform.position = lightCamera.transform.position;
            _colorRenderFillLight.transform.rotation = lightCamera.transform.rotation;
            _colorRenderFillLight.enabled = true;
            return true;
        }

        bool EnsureColorRenderFillLight()
        {
            if (_colorRenderFillLight != null)
            {
                return true;
            }

            GameObject fillLightObject = new GameObject("Ombarella Color Render Fill Light");
            fillLightObject.hideFlags = HideFlags.HideAndDontSave;
            fillLightObject.transform.SetParent(transform, false);
            _colorRenderFillLight = fillLightObject.AddComponent<Light>();
            _colorRenderFillLight.enabled = false;
            return true;
        }

        void DisableColorRenderFillLight()
        {
            if (_colorRenderFillLight != null)
            {
                _colorRenderFillLight.enabled = false;
            }
        }

        void DestroyColorRenderFillLight()
        {
            if (_colorRenderFillLight == null)
            {
                return;
            }

            Destroy(_colorRenderFillLight.gameObject);
            _colorRenderFillLight = null;
        }

        void HandleAsyncScoreReadback(AsyncGPUReadbackRequest request, AsyncScoreSample sample, LightMeterRenderPass renderPass)
        {
            AsyncScoreBatch batch = sample.Batch;
            if (_isDestroyed || batch == null || batch.Id != _asyncBatchId)
            {
                return;
            }

            try
            {
                if (!request.hasError)
                {
                    if (renderPass == LightMeterRenderPass.Luma)
                    {
                        sample.LumaPassFailed = !TryCalculateLumaStats(request.GetData<Color32>(), batch.Settings, out sample.LumaStats);
                    }
                    else
                    {
                        sample.ColorPassFailed = !TryCalculateColorBreadthStats(request.GetData<Color32>(), batch.Settings, out sample.ColorStats);
                    }
                }
                else if (renderPass == LightMeterRenderPass.Luma)
                {
                    sample.LumaPassFailed = true;
                }
                else
                {
                    sample.ColorPassFailed = true;
                }
            }
            catch (Exception e)
            {
                Utils.LogError($"Ombarella async readback failed for {renderPass}: {e}");
                if (renderPass == LightMeterRenderPass.Luma)
                {
                    sample.LumaPassFailed = true;
                }
                else
                {
                    sample.ColorPassFailed = true;
                }
            }

            sample.Pending--;
            batch.Pending--;
            if (sample.Pending == 0 && !sample.LumaPassFailed && !sample.ColorPassFailed && sample.LumaStats != null && sample.ColorStats != null)
            {
                batch.ScoreSum += CombineTwoPassScore(sample.LumaStats, sample.ColorStats, batch.Settings);
                batch.ScoreCount++;
            }

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
                RedColorBreadthMulti = RedColorBreadthMulti.Value,
                GreenColorBreadthMulti = GreenColorBreadthMulti.Value,
                BlueColorBreadthMulti = BlueColorBreadthMulti.Value,
                LumaColorDistribution = LumaColorDistribution.Value
            };
        }

        bool TryCalculateLumaStats(NativeArray<Color32> pixels, ScoreSettings settings, out RenderStats stats)
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
                AccumulateLumaStats(stats, pixel.r, pixel.g, pixel.b, settings);
            }

            return FinalizeLumaStats(stats);
        }

        bool TryCalculateLumaStats(Color[] pixels, ScoreSettings settings, out RenderStats stats)
        {
            stats = new RenderStats();
            if (pixels == null || pixels.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < pixels.Length; i++)
            {
                Color pixel = pixels[i];
                AccumulateLumaStats(stats, pixel.r * 255f, pixel.g * 255f, pixel.b * 255f, settings);
            }

            return FinalizeLumaStats(stats);
        }

        void AccumulateLumaStats(RenderStats stats, float r, float g, float b, ScoreSettings settings)
        {
            stats.SampleCount++;
            stats.RLumaSum += r * settings.RedLumaMulti;
            stats.GLumaSum += g * settings.GreenLumaMulti;
            stats.BLumaSum += b * settings.BlueLumaMulti;
        }

        bool FinalizeLumaStats(RenderStats stats)
        {
            if (stats.SampleCount == 0)
            {
                return false;
            }

            stats.Luma = (stats.RLumaSum + stats.GLumaSum + stats.BLumaSum) / (255f * stats.SampleCount);
            return !float.IsNaN(stats.Luma) && !float.IsInfinity(stats.Luma);
        }

        bool TryCalculateColorBreadthStats(NativeArray<Color32> pixels, ScoreSettings settings, out RenderStats stats)
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
                AccumulateColorBreadthStats(stats, pixel.r, pixel.g, pixel.b);
            }

            return FinalizeColorBreadthStats(stats, settings);
        }

        bool TryCalculateColorBreadthStats(Color[] pixels, ScoreSettings settings, out RenderStats stats)
        {
            stats = new RenderStats();
            if (pixels == null || pixels.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < pixels.Length; i++)
            {
                Color pixel = pixels[i];
                AccumulateColorBreadthStats(stats, pixel.r * 255f, pixel.g * 255f, pixel.b * 255f);
            }

            return FinalizeColorBreadthStats(stats, settings);
        }

        void AccumulateColorBreadthStats(RenderStats stats, float r, float g, float b)
        {
            stats.SampleCount++;

            if (r < stats.RLow) stats.RLow = r;
            if (g < stats.GLow) stats.GLow = g;
            if (b < stats.BLow) stats.BLow = b;

            if (r > stats.RHigh) stats.RHigh = r;
            if (g > stats.GHigh) stats.GHigh = g;
            if (b > stats.BHigh) stats.BHigh = b;
        }

        bool FinalizeColorBreadthStats(RenderStats stats, ScoreSettings settings)
        {
            if (stats.SampleCount == 0)
            {
                return false;
            }

            float redBreadth = (stats.RHigh - stats.RLow) * settings.RedColorBreadthMulti;
            float greenBreadth = (stats.GHigh - stats.GLow) * settings.GreenColorBreadthMulti;
            float blueBreadth = (stats.BHigh - stats.BLow) * settings.BlueColorBreadthMulti;
            stats.ColorBreadth = Mathf.Clamp01((redBreadth + greenBreadth + blueBreadth) / (255f * 3f));
            return !float.IsNaN(stats.ColorBreadth) && !float.IsInfinity(stats.ColorBreadth);
        }

        float CombineTwoPassScore(RenderStats lumaStats, RenderStats colorStats, ScoreSettings settings)
        {
            float lumaScore = Mathf.Clamp01(lumaStats.Luma * settings.LumaCoef);
            float colorBreadthScore = Mathf.Clamp01(colorStats.ColorBreadth);
            float colorWeight = Mathf.Clamp01(settings.LumaColorDistribution);
            float lumaWeight = 1f - colorWeight;
            float score = lumaScore * lumaWeight + colorBreadthScore * colorWeight;

            debugLumaScore = lumaScore;
            debugScore2 = colorBreadthScore;

            return Mathf.Clamp(score, 0.01f, 1f);
        }

        enum LightMeterRenderPass
        {
            Luma,
            ColorBreadth
        }

        class AsyncScoreBatch
        {
            public int Id;
            public int Pending;
            public int ScoreCount;
            public float ScoreSum;
            public ScoreSettings Settings;
        }

        class AsyncScoreSample
        {
            public AsyncScoreBatch Batch;
            public int Pending;
            public bool LumaPassFailed;
            public bool ColorPassFailed;
            public RenderStats LumaStats;
            public RenderStats ColorStats;
        }

        struct ScoreSettings
        {
            public float LumaCoef;
            public float RedLumaMulti;
            public float GreenLumaMulti;
            public float BlueLumaMulti;
            public float RedColorBreadthMulti;
            public float GreenColorBreadthMulti;
            public float BlueColorBreadthMulti;
            public float LumaColorDistribution;
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
            public float ColorBreadth;
        }

        sealed class MagnifiedOpticRenderScope : IDisposable
        {
            struct RendererState
            {
                public Renderer Renderer;
                public bool ForceRenderingOff;
            }

            readonly List<RendererState> _rendererStates = new List<RendererState>();
            bool _disposed;

            public HashSet<Renderer> HiddenRenderers { get; private set; }

            public MagnifiedOpticRenderScope(HashSet<Renderer> renderers)
            {
                HiddenRenderers = renderers ?? new HashSet<Renderer>();

                foreach (Renderer renderer in HiddenRenderers)
                {
                    if (renderer == null)
                    {
                        continue;
                    }

                    _rendererStates.Add(new RendererState
                    {
                        Renderer = renderer,
                        ForceRenderingOff = renderer.forceRenderingOff
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
                    if (state.Renderer != null)
                    {
                        state.Renderer.forceRenderingOff = state.ForceRenderingOff;
                    }
                }

                _disposed = true;
            }
        }

        sealed class FirstPersonBodyRenderScope : IDisposable
        {
            struct RendererState
            {
                public Renderer Renderer;
                public ShadowCastingMode ShadowCastingMode;
            }

            readonly List<RendererState> _rendererStates = new List<RendererState>();
            bool _disposed;

            public FirstPersonBodyRenderScope(List<Renderer> renderers)
            {
                if (renderers == null)
                {
                    return;
                }

                for (int i = 0; i < renderers.Count; i++)
                {
                    Renderer renderer = renderers[i];
                    if (renderer == null)
                    {
                        continue;
                    }

                    _rendererStates.Add(new RendererState
                    {
                        Renderer = renderer,
                        ShadowCastingMode = renderer.shadowCastingMode
                    });
                    renderer.shadowCastingMode = ShadowCastingMode.On;
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
                    if (state.Renderer != null)
                    {
                        state.Renderer.shadowCastingMode = state.ShadowCastingMode;
                    }
                }

                _disposed = true;
            }
        }

        int _handleMain;


        float _avgLightMeter = 1f;
        public float FinalLightMeter = 1f;


        float _finalValueLerped = 1f;

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
            if (MasterSwitch == null || !MasterSwitch.Value)
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
                        string debugString = string.Format($"score {debugScore}, luma {debugLumaScore}, color breadth {debugScore2}");
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
            Color previousColor = GUI.color;
            DrawRenderTexturePreviewFrame(new Rect(20f, 80f, previewSize, previewSize), _rt);
            DrawRenderTexturePreviewFrame(new Rect(28f + previewSize, 80f, previewSize, previewSize), _colorRt);
            GUI.color = previousColor;
        }

        void DrawRenderTexturePreviewFrame(Rect previewRect, RenderTexture renderTexture)
        {
            if (renderTexture == null)
            {
                return;
            }

            Rect frameRect = new Rect(previewRect.x - 2f, previewRect.y - 2f, previewRect.width + 4f, previewRect.height + 4f);
            GUI.color = Color.black;
            GUI.DrawTexture(frameRect, Texture2D.whiteTexture, ScaleMode.StretchToFill);
            GUI.color = Color.white;
            GUI.DrawTexture(previewRect, renderTexture, ScaleMode.ScaleToFit, false);
        }

    }
}
