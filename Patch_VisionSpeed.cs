using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

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
                if (__instance != null)
                {
                    __result *= Plugin.Instance.FinalLightMeter;
                }
            }
        }
    }
}