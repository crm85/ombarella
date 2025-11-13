using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;
using EFT;

namespace ombarella
{
    public static class CameraRig
    {
        public static Camera _lightCam;
        public static void Initialize(Camera camera)
        {
            _lightCam = camera;
        }
        public static void RepositionCamera(List<Player> targetList)
        {
            Player player = Utils.GetMainPlayer();

            if (targetList.Count == 1 && targetList[0] == player)
            {
                return;
            }

            float distance = 0;
            Player closestBot = null;
            foreach (var bot in targetList)
            {
                if (bot == Utils.GetMainPlayer())
                {
                    continue;
                }
                else if (distance == 0)
                {
                    distance = Vector3.Distance(bot.Position, player.Position);
                    closestBot = bot;
                    continue;
                }
                else
                {
                    float lastDistance = Vector3.Distance(bot.Position, player.Position);
                    if (lastDistance < distance)
                    {
                        distance = lastDistance;
                        closestBot = bot;
                    }
                }
            }

            if (closestBot == null) return;

            Vector3 newCamPos = closestBot.PlayerBones.Head.position;
            Vector3 playerPosAdjusted = player.PlayerBody.PlayerBones.Ribcage.position;

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
        }
    }
}
