using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Reflection;
using UnityEngine;

namespace ombarella
{
    public class Patch_AimOffset : ModulePatch
    {
        static readonly FieldInfo AimOffsetField = AccessTools.Field(typeof(BotAimingClass), "float_13") ?? AccessTools.Field(typeof(BotAimingClass), "Float_13");
        static readonly FieldInfo AimDirectionField = AccessTools.Field(typeof(BotAimingClass), "vector3_4") ?? AccessTools.Field(typeof(BotAimingClass), "Vector3_4");

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(BotAimingClass), "method_13");
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

                if (AimOffsetField == null || AimDirectionField == null)
                {
                    return;
                }

                // modify aim offset
                float aimOffset = (float)AimOffsetField.GetValue(__instance);
                float aimOffsetCoef = (1f - Plugin.Instance.FinalLightMeter) * 100f;
                aimOffsetCoef *= Plugin.AimNerf.Value;
                aimOffset *= aimOffsetCoef;
                Vector3 vector3_ = (Vector3)AimDirectionField.GetValue(__instance);
                Vector3 endTargetPoint = __instance.RealTargetPoint + vector3_ * aimOffset;
                __instance.EndTargetPoint = endTargetPoint;
            }
        }
    }
}
