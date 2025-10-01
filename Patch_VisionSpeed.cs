using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using UnityEngine;

namespace ombarella
{
    public class Patch_VisionSpeed : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(EnemyInfo), nameof(EnemyInfo.method_7));
        }

        [PatchPostfix]
        public static void PatchPostfix(ref float __result, EnemyInfo __instance)
        {
            {
                if (!Plugin.MasterSwitch.Value)
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