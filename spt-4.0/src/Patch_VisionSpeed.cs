using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Reflection;
using UnityEngine;

namespace ombarella
{
    public class Patch_VisionSpeed : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(
                typeof(EnemyInfo),
                "method_9",
                new[]
                {
                    typeof(BotDifficultySettingsClass),
                    typeof(IAIData),
                    typeof(float),
                    typeof(Vector3),
                    typeof(float),
                    typeof(float),
                    typeof(float)
                }) ?? throw new MissingMethodException("Unable to find SPT4 EnemyInfo.method_9 visibility-speed multiplier.");
        }

        [PatchPostfix]
        public static void PatchPostfix(ref float __result, EnemyInfo __instance)
        {
            {
                if (Plugin.Instance == null || !Plugin.Instance.CanApplyBotVisibilityPatch())
                {
                    return;
                }
                if (__instance == null)
                {
                    return ;
                }

                // modify vision speed
                float newValue = Plugin.Instance.FinalLightMeter;
                
                if (__instance.HaveNightVision())
                {
                    newValue = Mathf.Lerp(newValue, 1f, 0.7f);
                }
                __result *= newValue;
            }
        }
    }
}
