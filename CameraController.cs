using UnityEngine;
using Random = UnityEngine.Random;

namespace ombarella
{
    public static class CameraController
    {
        public static Camera _lightCam;
        public static void Initialize(Camera camera)
        {
            _lightCam = camera;
        }
        public static void RepositionCamera()
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

            Utils.DrawDebugLine(newCamPos, newCamPos + vectorCameraToPlayer);
        }
    }
}
