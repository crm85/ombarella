using System;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;
using Object = UnityEngine.Object;

namespace ombarella
{
    public class LightCamera : MonoBehaviour
    {
        LightCamera Instance;

        //
        // camera vars
        //
        Camera _lightCam;
        RenderTexture rt;
        Texture2D tex;
        Rect rectReadPicture;
        readonly int rectSize = 8;

        //
        // light meter vars
        //
        float _lightMeterPool = 0.01f;
        float _lightMeterAverage = 0.01f;
        float _lightMeterClamped = 0.01f;

        public float LightMeterFinalValue
        {
            get { return _lightMeterClamped; }
        }

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            Initialize();
        }

        void Initialize()
        {
            SetupCamera();
        }


        void SetupCamera()
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

        public void RepositionCamera()
        {
            Vector3 playerPosAdjusted = Utils.GetMainPlayer().Position;
            playerPosAdjusted.y += Plugin.AddYPlayer.Value;
            float randomAngle = Random.Range(1f, 360f);
            Quaternion cameraAngleYAxis = Quaternion.AngleAxis(randomAngle, Vector3.up);
            Vector3 posOffsetFromPlayer = playerPosAdjusted;
            posOffsetFromPlayer.x += Plugin.CamDistanceOffset.Value;
            Vector3 direction = posOffsetFromPlayer - playerPosAdjusted;
            Vector3 rotDirection = cameraAngleYAxis * direction;
            Vector3 newCamPos = playerPosAdjusted + rotDirection;
            newCamPos.y += Plugin.CamVerticalOffset.Value;
            Vector3 vectorCameraToPlayer = playerPosAdjusted - newCamPos;
            _lightCam.gameObject.transform.position = newCamPos;
            _lightCam.gameObject.transform.rotation = Quaternion.LookRotation(vectorCameraToPlayer);
        }

        private void UpdateLightMeter()
        {
            _lightCam.fieldOfView = Plugin.CameraFOV.Value;
            RepositionCamera();
            AddMeasurementToAverage(MeasureRenderTex());
            ClampFinalValue();
        }

        

        void AddMeasurementToAverage(float newMeasurement)
        {
            newMeasurement = Mathf.Clamp(newMeasurement, 0.01f, 0.1f);
            newMeasurement *= Plugin.MeterMulti.Value;
            // here the average changes, which is the principal output of the mod
            float ratio = Plugin.SamplesPerSecond.Value;
            _lightMeterPool -= _lightMeterAverage;
            _lightMeterPool = Mathf.Clamp(_lightMeterPool, 0.01f, 10f);
            _lightMeterPool += newMeasurement;
            _lightMeterAverage = _lightMeterPool / ratio;
            Utils.Log($"_lightMeterPool : {_lightMeterPool} || newMeasurement {newMeasurement} || _avgLightMeter : {_lightMeterAverage}", true);
        }

        void ClampFinalValue()
        {
            float finalValue = _lightMeterAverage * Plugin.DetectionMultiplier.Value;
            finalValue *= Mathf.Clamp(_lightMeterAverage, 0.1f, 1f);
            _lightMeterClamped = finalValue;
            //Utils.Log($"final light output : {FinalLightMeter}", true);
        }

        float MeasureRenderTex()
        {
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
