using BepInEx;
using BepInEx.Configuration;
using EFT;
using SPT.Reflection.Patching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using static System.Net.Mime.MediaTypeNames;

namespace ombarella
{
    [BepInPlugin(modGUID, modName, modVersion)]
    public class Plugin : BaseUnityPlugin
    {
        const string modGUID = "Ombarella";
        const string modName = "Ombarella";
        const string modVersion = "0.1";

        Camera _sampleCam;
        Material _renderMat;
        RenderTexture _renderTex;
        public static Plugin Instance;

        void Awake()
        {
            Instance = this;
            Initialize();
        }

        Player _mainPlayer;
        bool _raidRunning = false;
        float _seenCoefModified = 0;



        public static ConfigEntry<float> SightDebug;
        public static ConfigEntry<float> AddX;
        public static ConfigEntry<float> AddY;
        public static ConfigEntry<float> AddZ;
        public static ConfigEntry<float> SamplesPerSecond;


        void Initialize()
        {
            Utils.Logger = this.Logger;

            SetupCamera();
            //_sampleCam = new Camera();
            //_renderMat = new Material(Shader.Find("Standard"));
            //_renderTex = new RenderTexture(200, 200, 5);
            //int nameID = _renderTex.GetInstanceID();
            //_renderMat.SetTexture(nameID, _renderTex);

            /*
             * Render Textures seem the way to go, 
             * you can get a Camera to Render to it 
             * then have a C# script go over the pixels
             * and calculate either Value that is the 
             * highest channel of the 3 or Luminosity 
             * and that is something like (r * .2) + (g * .7) + (b * .1)
             * and that will give you human perceived brightness.
             */
            _mainPlayer = Utils.GetMainPlayer();

            TryLoadPatch(new Patch_VisionSpeed());

            LoadConfig();
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
            SightDebug = ConstructFloatConfig(1.0f, "z - Debug", "Sight Debug", "", 0.01f, 10f);
            AddX = ConstructFloatConfig(0, "z - Debug", "AddX", "", 0, 10f);
            AddY = ConstructFloatConfig(0, "z - Debug", "AddY", "", 0, 10f);
            AddZ = ConstructFloatConfig(0, "z - Debug", "AddZ", "", 0, 10f);
            SamplesPerSecond = ConstructFloatConfig(1f, "a - Settings", "Samples per second", "", 1f, 20f);
        }

        void Update()
        {
            UpdateLight();
            AdjustCamera();
            //if (_raidRunning)
            //{

            //}
        }

        void SetCameraToPlayer()
        {
            _sampleCam.transform.position = _mainPlayer.Position;
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


        public static readonly LayerMask PlayerCollisionsMask = GClass1403.GetAllCollisionsLayerMask(LayerMaskClass.PlayerLayer);


        private Texture2D tex;
        private Rect rectReadPicture;
        Camera _lightCam;
        RenderTexture rt;
        int rectSize = 8;

        private void SetupCamera()
        {
            _lightCam = new Camera();
            _lightCam = gameObject.AddComponent<Camera>();
            rt = new RenderTexture(rectSize, rectSize, 0);
            rt.dimension = TextureDimension.Tex2D;
            rt.wrapMode = TextureWrapMode.Clamp;
            _lightCam.targetTexture = rt;
            tex = new Texture2D(rt.width, rt.height);
            rectReadPicture = new Rect(0, 0, rt.width, rt.height);
        }

        Player _player;
        private void UpdateLight()
        {
            if (_player == null)
            {
                _player = Utils.GetMainPlayer();
            }
            if (_player == null)
            {
                return;
            }

            AdjustCamera();
            // MeasureSinglePixel();
            if (IsReadyForUpdate())
            {
                MeasureAllPixels();
            }
        }

        void AdjustCamera()
        {
            if (_player == null)
            { return; }
            // _lightCam.gameObject.transform.position = _cameraPos;
            // _lightCam.gameObject.transform.rotation = Quaternion.Euler(_cameraRot);
            Vector3 relativePos = _player.Position - transform.position;
            _lightCam.gameObject.transform.rotation = Quaternion.LookRotation(relativePos);
            Vector3 newPos = _player.Position;
            newPos.x += Plugin.AddX.Value;
            newPos.y += Plugin.AddY.Value;
            newPos.z += Plugin.AddZ.Value;
            _lightCam.gameObject.transform.position = newPos;
        }
        

        float _timer = 0;
        float _updateFrequency = 30f;
        bool IsReadyForUpdate()
        {
            _timer += Time.deltaTime;
            float limit = SamplesPerSecond.Value / 1f;
            if (_timer > limit)
            {
                _timer = _timer - limit;
                return true;
            }
            return false;
        }

        void MeasureAllPixels()
        {
            float allGray = 0;
            RenderTexture.active = rt;
            for (int i = 0; i < rt.width; i++)
            {
                for (int j = 0; j < rt.height; j++)
                {
                    tex.ReadPixels(rectReadPicture, i, j);
                    tex.Apply();

                    // Debug.Log(tex.GetPixel(i, j).grayscale);
                    float gray = tex.GetPixel(i, j).grayscale;
                    allGray += gray;
                }
            }
            RenderTexture.active = null;
            rt.Release();

            float finalAverage = allGray / (rt.width * rt.height);
            Debug.Log($"final average = {finalAverage}");
            LightMeasure = finalAverage;
            Utils.Log($"light measure = {finalAverage}");
        }

        public float LightMeasure = 1f;
    }
}