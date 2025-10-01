using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Reflection;
using UnityEngine;

namespace ombarella
{
    public class Patch_AimOffset : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(BotAimingClass), "method_13", (Type[])null, (Type[])null);
        }

        [PatchPostfix]
        public static void PatchPostfix(BotAimingClass __instance)
        {
            {
                if (!Plugin.MasterSwitch.Value)
                {
                    return;
                }
                if (__instance == null)
                {
                    return;
                }

                // modify aim offset
                float aimOffset = __instance.float_13;
                float aimOffsetCoef = (1f - Plugin.Instance.FinalLightMeter) * 100f;
                aimOffsetCoef *= Plugin.AimNerf.Value;
                aimOffset *= aimOffsetCoef;
                Vector3 vector3_ = __instance.vector3_4;
                Vector3 endTargetPoint = __instance.RealTargetPoint + vector3_ * aimOffset;
                __instance.EndTargetPoint = endTargetPoint;
            }
        }
    }
}
