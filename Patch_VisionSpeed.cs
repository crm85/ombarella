using HarmonyLib;
using EFT;
using SPT.Reflection.Patching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

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
            //if (SAINEnableClass.GetSAIN(__instance.Owner.ProfileId, out var sain))
            {
                //Enemy enemy = sain.EnemyController.GetEnemy(__instance.ProfileId, false);
                //enemy ??= sain.EnemyController.CheckAddEnemy(__instance.Person);
                if (__instance != null)
                {
                    //float sainMod = EnemyGainSightClass.GetGainSightModifier(enemy);
                    //__result /= sainMod;
                    //enemy.Vision.LastGainSightResult = __result;
                    __result *= Plugin.SightDebug.Value;
                    __result *= Plugin.Instance.LightMeasure;
                }
            }
        }
    }
}
