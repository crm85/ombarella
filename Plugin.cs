using BepInEx;
using EFT;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

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
        void Initialize()
        {
            _sampleCam = new Camera();
            _renderMat = new Material(Shader.Find("Standard"));
            _renderTex = new RenderTexture(200, 200, 5);
            int nameID = _renderTex.GetInstanceID();
            _renderMat.SetTexture(nameID, _renderTex);
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
        }

        void Update()
        {
            SetCameraToPlayer();
        }

        void SetCameraToPlayer()
        {
            _sampleCam.transform.position = _mainPlayer.Position;
        }
    }
}