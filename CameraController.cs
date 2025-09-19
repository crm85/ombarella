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
            playerPosAdjusted.y += Plugin.Instance.AddYPlayer;
            float randomAngle = Random.Range(1f, 360f);
            Quaternion cameraAngleYAxis = Quaternion.AngleAxis(randomAngle, Vector3.up);
            Vector3 posOffsetFromPlayer = playerPosAdjusted;
            posOffsetFromPlayer.x += Plugin.Instance.CamDistanceOffset;
            Vector3 direction = posOffsetFromPlayer - playerPosAdjusted;
            Vector3 rotDirection = cameraAngleYAxis * direction;
            Vector3 newCamPos = playerPosAdjusted + rotDirection;
            newCamPos.y += Plugin.Instance.CamVerticalOffset;
            Vector3 vectorCameraToPlayer = playerPosAdjusted - newCamPos;
            _lightCam.gameObject.transform.position = newCamPos;
            _lightCam.gameObject.transform.rotation = Quaternion.LookRotation(vectorCameraToPlayer);
            linePos1 = newCamPos;
            linePos2 = newCamPos + vectorCameraToPlayer;
        }

        static Vector3 linePos1 = Vector3.zero;
        static Vector3 linePos2 = Vector3.zero;

        public static void UpdateDebugLines()
        {
            Utils.DrawDebugLine(linePos1, linePos2);

        }
    }
}
