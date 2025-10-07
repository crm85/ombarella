using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;
using EFT;

namespace ombarella
{
    public static class CameraRig
    {
        public static Camera _lightCam;
        static int targetIterator = 0;
        public static void Initialize(Camera camera)
        {
            _lightCam = camera;
        }
        public static void RepositionCamera(List<Player> targetList)
        {
            if (targetList.Count == 1 && targetList[0] == Utils.GetMainPlayer())
            {
                return;
            }
            if (targetList.Count <= targetIterator)
            {
                targetIterator = 0;
            }
            if (targetList[targetIterator] == Utils.GetMainPlayer())
            {
                targetIterator++;
            }

            Vector3 newCamPos = targetList[targetIterator].PlayerBones.Head.position;
            Vector3 playerPosAdjusted = Utils.GetMainPlayer().PlayerBody.PlayerBones.Ribcage.position;

            //
            // old random logic
            //

            //float randomAngle = Random.Range(1f, 360f);
            //Quaternion cameraAngleYAxis = Quaternion.AngleAxis(randomAngle, Vector3.up);
            //Vector3 posOffsetFromPlayer = playerPosAdjusted;
            //posOffsetFromPlayer.x += Plugin.CamHorizontalOffset.Value;
            //Vector3 cameraOffsetFromPlayer = posOffsetFromPlayer - playerPosAdjusted;
            //Vector3 rotDirection = cameraAngleYAxis * cameraOffsetFromPlayer;
            //Vector3 newCamPos = playerPosAdjusted + rotDirection;

            Vector3 vectorCameraToPlayer = playerPosAdjusted - newCamPos;
            vectorCameraToPlayer = Vector3.ClampMagnitude(vectorCameraToPlayer, Plugin.CamHorizontalOffset.Value);
            newCamPos = playerPosAdjusted + -vectorCameraToPlayer;
            _lightCam.gameObject.transform.position = newCamPos;
            _lightCam.gameObject.transform.rotation = Quaternion.LookRotation(vectorCameraToPlayer);

            targetIterator++;
        }
    }
}
