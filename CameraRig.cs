using System.Collections.Generic;
using EFT;
using UnityEngine;

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
            Vector3 focusPoint;
            return TryRepositionCamera(player, observerList, out focusPoint);
        }

        public static bool TryRepositionCamera(Player player, List<Player> observerList, out Vector3 focusPoint)
        {
            focusPoint = Vector3.zero;
            if (!Utils.IsLightMeterUsablePlayer(player) || observerList == null || observerList.Count == 0 || _lightCam == null)
            {
                return false;
            }

            float closestDistance = float.MaxValue;
            Player closestBot = null;
            foreach (Player bot in observerList)
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

            focusPoint = GetPlayerFocusPoint(player);
            Vector3 observerToFocus = focusPoint - closestBot.PlayerBones.Head.position;
            if (observerToFocus.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            observerToFocus = Vector3.ClampMagnitude(observerToFocus, Plugin.CamHorizontalOffset.Value);
            Vector3 cameraPosition = focusPoint - observerToFocus;
            _lightCam.gameObject.transform.position = cameraPosition;
            _lightCam.gameObject.transform.rotation = Quaternion.LookRotation(focusPoint - cameraPosition);
            return true;
        }

        public static bool TryRepositionCameraOnOrbit(Player player, float radius, float heightOffset, float angleDegrees, out Vector3 focusPoint)
        {
            focusPoint = Vector3.zero;
            if (!Utils.IsLightMeterUsablePlayer(player) || _lightCam == null)
            {
                return false;
            }

            focusPoint = GetPlayerFocusPoint(player);
            if (!Utils.IsFinite(focusPoint))
            {
                return false;
            }

            radius = Mathf.Max(0.1f, radius);
            Vector3 orbitOffset = Quaternion.Euler(0f, angleDegrees, 0f) * (Vector3.forward * radius);
            Vector3 cameraPosition = focusPoint + orbitOffset + Vector3.up * heightOffset;
            Vector3 vectorCameraToPlayer = focusPoint - cameraPosition;
            if (vectorCameraToPlayer.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            _lightCam.gameObject.transform.position = cameraPosition;
            _lightCam.gameObject.transform.rotation = Quaternion.LookRotation(vectorCameraToPlayer);
            return true;
        }

        public static Vector3 GetPlayerFocusPoint(Player player)
        {
            return player.PlayerBones.Ribcage.position + Vector3.up * Plugin.CameraFocusHeightOffset.Value;
        }
    }
}
