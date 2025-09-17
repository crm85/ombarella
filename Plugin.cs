using BepInEx;
using BepInEx.Configuration;
using EFT;
using SPT.Reflection.Patching;
using System;
using UnityEngine;
using UnityEngine.Rendering;
using Unity;
using Random = UnityEngine.Random;
using Object = UnityEngine.Object;

namespace ombarella
{
    [BepInPlugin(modGUID, modName, modVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance;

        const string modGUID = "Ombarella";
        const string modName = "Ombarella";
        const string modVersion = "0.1";

        Player _player;
        Camera _lightCam;
        RenderTexture rt;
        Texture2D tex;
        Rect rectReadPicture;
        int rectSize = 8;
        bool _isRaidRunning = false;

        void Awake()
        {
            Instance = this;
            Initialize();
        }

        // config values
        public static ConfigEntry<float> DetectionMultiplier;
        public static ConfigEntry<float> SamplesPerSecond;
        public static ConfigEntry<float> SampleRandomizer;
        public static ConfigEntry<bool> MeterViz;

        // debug values
        public static ConfigEntry<float> CamDistanceOffset;
        public static ConfigEntry<float> CamVerticalOffset;
        public static ConfigEntry<float> AddZCam;
        public static ConfigEntry<float> AddYPlayer;
        public static ConfigEntry<float> CameraFOV;
        public static ConfigEntry<float> MeterMulti;

        public static ConfigEntry<float> DebugUpdateFreq;
        public static ConfigEntry<bool> IsDebug;

        static Vector3 _rigPos1 = new Vector3(1f, 0.2f, 0);
        static Vector3 _rigPos2 = new Vector3(-0.5f, 0.2f, -1f);
        static Vector3 _rigPos3 = new Vector3(-0.5f, 0.2f, 1f);
        static int _rigCount = 1;

        PluginManager Manager { get; set; }
        Visualiser Visualiser { get; set; }

        void Initialize()
        {
            Manager = new PluginManager();
            Visualiser = new Visualiser();

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
            SamplesPerSecond = ConstructFloatConfig(1.5f, "a - Settings", "Samples per second", "How many times per second the light meter is sampled; affects performance a lot", 1f, 20f);
            DetectionMultiplier = ConstructFloatConfig(1.0f, "a - Settings", "Light meter strength", "Modify how much bots are affected by the light meter (lower = you become harder to detect in low light)", 0.1f, 1f);
            SampleRandomizer = ConstructFloatConfig(0.15f, "a - Settings", "Sample Randomizer", "Percentage change that pixel analysis will be skipped on a given pass; essentially a performance tool. 0 = zero % chance of sample, 1 = guaranteed sample", 0f, 1f);
            CameraFOV = ConstructFloatConfig(90f, "a - Settings", "CameraFOV", "", 10f, 170f);
            MeterViz = ConstructBoolConfig(false, "a - Settings", "Enable light meter indicator", "Visual representation of how much you are being lit and how visible you are");
            
            IsDebug = ConstructBoolConfig(false, "y - Debug", "1) Enable debug logging", "");
            DebugUpdateFreq = ConstructFloatConfig(1f, "y - Debug", "2) Debug updates per second", "How frequently the debug logger updates per second", 1f, 10f);

            //
            // for eventual deletion
            //
            CamDistanceOffset = ConstructFloatConfig(0, "z - Debug", "CamDistanceOffset", "", -1f, 1f);
            CamVerticalOffset = ConstructFloatConfig(0, "z - Debug", "CamVerticalOffset", "", -1f, 1f);
            AddZCam = ConstructFloatConfig(0, "z - Debug", "AddZCam", "", -1f, 1f);
            AddYPlayer = ConstructFloatConfig(0, "z - Debug", "AddYPlayer", "", 0, 10f);
            MeterMulti = ConstructFloatConfig(10f, "z - Debug", "meterMulti", "", 1f, 20f);

        }

        void Update()
        {
            Utils.UpdateDebug(Time.deltaTime);
            //if (!Utils.IsInRaid())
            //{
            //    return;
            //}
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
            tex = new Texture2D(rt.width, rt.height);
            rectReadPicture = new Rect(0, 0, rt.width, rt.height);
            CameraController.Initialize(_lightCam);
            _lightCam.enabled = false;
        }

        private void UpdateLightMeter()
        {
            _lightCam.fieldOfView = CameraFOV.Value;
            if (IsReadyForUpdate())
            {
                Utils.Log("ready for update", false);
                RepositionCamera();
                AddMeasurementToAverage(MeasureRenderTex());
                ClampFinalValue();
            }
        }

        float _lightMeterPool = 0f;
        float _avgLightMeter = 0.01f;
        public float FinalLightMeter = 0.01f;

        void AddMeasurementToAverage(float newMeasurement)
        {
            newMeasurement = Mathf.Clamp(newMeasurement, 0.01f, 0.1f);
            newMeasurement *= MeterMulti.Value;
            // here the average changes, which is the principal output of the mod
            float ratio = SamplesPerSecond.Value;
            _lightMeterPool -= _avgLightMeter;
            _lightMeterPool = Mathf.Clamp(_lightMeterPool, 0.01f, 10f);
            _lightMeterPool += newMeasurement;
            _avgLightMeter = _lightMeterPool / ratio;
            Utils.Log($"_lightMeterPool : {_lightMeterPool} || newMeasurement {newMeasurement} || _avgLightMeter : {_avgLightMeter}", true);
        }

        void ClampFinalValue()
        {
            float finalValue = _avgLightMeter * DetectionMultiplier.Value;
            finalValue *= Mathf.Clamp(_avgLightMeter, 0.1f, 1f);
            FinalLightMeter = finalValue;
            //Utils.Log($"final light output : {FinalLightMeter}", true);
        }

        void RepositionCamera()
        {
            //Utils.Log("camera repos", false);

            //_rigCount++;
            //if (_rigCount > 3)
            //{
            //    _rigCount = 1;
            //}
            //Vector3 rigNewPos;
            //switch (_rigCount)
            //{
            //    case 1:
            //        rigNewPos = _rigPos1;
            //        break;
            //    case 2:
            //        rigNewPos = _rigPos2;
            //        break;
            //    default:
            //        rigNewPos = _rigPos3;
            //        break;
            //}


            //Vector3 playerPos = _player.Position;
            //playerPos.y += AddYPlayer.Value;
            //Vector3 newCamPos = playerPos;
            ////Vector3 newCamPos = playerPos + rigNewPos;
            //newCamPos.x += Plugin.CamDistanceOffset.Value;
            //newCamPos.y += Plugin.CamVerticalOffset.Value;
            //newCamPos.z += Plugin.AddZCam.Value;
            //Vector3 vectorCameraToPlayer = playerPos - newCamPos;

            //_lightCam.gameObject.transform.rotation = Quaternion.LookRotation(vectorCameraToPlayer);
            //_lightCam.gameObject.transform.position = newCamPos;

            CameraController.RepositionCamera();
        }
        
        float _timer = 0;
        bool IsReadyForUpdate()
        {
            _timer += Time.deltaTime;
            float limit = 1f / SamplesPerSecond.Value;
            if (_timer > limit)
            {
                _timer = _timer - limit;
                return true;
            }
            return false;
        }

        float GetLumen(Color input)
        {
            float R = input.r;
            float G = input.g;
            float B = input.b;
            float lumen = (0.299f * R + 0.587f * G + 0.114f * B);
            return lumen;
        }
        
        float MeasureRenderTex()
        {
            //int passes = 0;
            //float allLumen = 0;
            //RenderTexture.active = rt;

            //int middlePixels = Mathf.RoundToInt(rectSize / 2f);
            //tex.ReadPixels(rectReadPicture, middlePixels, middlePixels);
            //tex.Apply();
            //Color colorLumen = tex.GetPixel(middlePixels, middlePixels);
            //float lumen = GetLumen(colorLumen);
            //allLumen += lumen;
            //passes++;


            //Utils.Log($"lumen = {lumen}", true);

            ////for (int i = 0; i < rt.width; i++)
            ////{
            ////    for (int j = 0; j < rt.height; j++)
            ////    {
            ////        float random = Random.Range(0.0f, 1.0f);
            ////        if (random < SampleRandomizer.Value)
            ////        {
            ////            Utils.Log($"taking sample", true);

            ////            tex.ReadPixels(rectReadPicture, i, j);
            ////            tex.Apply();

            ////            float gray = tex.GetPixel(i, j).grayscale;
            ////            Utils.Log($"render tex full colours : {tex.GetPixel(i, j)}", true);
            ////            allGray += gray;
            ////            passes++;
            ////        }
            ////    }
            ////}
            //RenderTexture.active = null;
            //rt.Release();

            //float finalAverage = allLumen / passes;
            ////Utils.Log($"render tex reading: {finalAverage}", true);
            //return finalAverage;

            _lightCam.Render();

            var oldRenderTexture = RenderTexture.active;
            RenderTexture.active = rt;

            var texture2D = new Texture2D(rt.width, rt.height, TextureFormat.RGBAFloat, false);
            texture2D.ReadPixels(new Rect(0, 0, texture2D.width, texture2D.height), 0, 0, false);
            texture2D.Apply();

            Color[] allColors = texture2D.GetPixels();

            float totalLuminance = 0f;

            foreach (var color in allColors)
            {
                totalLuminance += (color.r * 0.2126f) + (color.g * 0.7152f) + (color.b * 0.0722f);
            }

            float averageLuminance = totalLuminance / allColors.Length;


            RenderTexture.active = oldRenderTexture;
            Object.Destroy(texture2D);

            Utils.Log($"lumen {averageLuminance}", true);

            return averageLuminance;
        }
    }
}