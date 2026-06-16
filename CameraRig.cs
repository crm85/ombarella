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
            TryRepositionCamera(player, targetList);
        }

        public static bool TryRepositionCamera(Player player, List<Player> observerList)
        {
            if (!Utils.IsLightMeterUsablePlayer(player) || observerList == null || observerList.Count == 0)
            {
                return false;
            }

            float closestDistance = float.MaxValue;
            Player closestBot = null;
            foreach (var bot in observerList)
            {
                if (bot == player || !Utils.IsLightMeterUsablePlayer(bot))
                {
                    continue;
                }

                float distance = Vector3.Distance(bot.Position, player.Position);
                if (float.IsNaN(distance) || float.IsInfinity(distance))
                {
                    continue;
                }

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestBot = bot;
                }
            }

            if (closestBot == null)
            {
                return false;
            }

            Vector3 newCamPos = closestBot.PlayerBones.Head.position;
            Vector3 playerPosAdjusted = player.PlayerBones.Ribcage.position;

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
            if (vectorCameraToPlayer.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            vectorCameraToPlayer = Vector3.ClampMagnitude(vectorCameraToPlayer, Plugin.CamHorizontalOffset.Value);
            newCamPos = playerPosAdjusted + -vectorCameraToPlayer;
            _lightCam.gameObject.transform.position = newCamPos;
            _lightCam.gameObject.transform.rotation = Quaternion.LookRotation(vectorCameraToPlayer);
            return true;
        }
    }
}
