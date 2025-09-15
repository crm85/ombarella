using BepInEx;
using BepInEx.Configuration;
using EFT;
using SPT.Reflection.Patching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

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

        void Awake()
        {
            Initialize();
        }
        Player _mainPlayer;
        bool _raidRunning = false;
        float _seenCoefModified = 0;




        public static ConfigEntry<float> SightDebug;


        void Initialize()
        {
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
        }

        void Update()
        {
            //SetCameraToPlayer();
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
    }
}