using System;
using System.Collections.Generic;
using System.Reflection;
using Comfort.Common;
using EFT;
using EFT.Animations;
using EFT.HealthSystem;
using EFT.InventoryLogic;
using SPT.Reflection.Patching;
using UnityEngine;

namespace ombarella
{
    internal class Patch_SeenCoef : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return ReflectionHelper.FindMethodByArgTypes(typeof(EnemyInfo), new Type[6]
            {
                typeof(BifacialTransform),
                typeof(BifacialTransform),
                typeof(BotDifficultySettingsClass),
                typeof(IAIData),
                typeof(float),
                typeof(Vector3)
            });
        }

        [PatchPostfix]
        public static void PatchPostfix(EnemyInfo __instance, BifacialTransform BotTransform, BifacialTransform enemy, float personalLastSeenTime, Vector3 personalLastSeenPos, ref float __result)
        {
            /*
             * if (player.DebugInfo != null)
		    {
			    if (ShouldLogSeenCoef())
			    {
				    player.DebugInfo.lastCalcTo8 = __result;
				    player.DebugInfo.lastFactor2 = num34;
				    player.DebugInfo.rawTerrainScoreSample = num46;
			    }
			    player.DebugInfo.calced++;
			    player.DebugInfo.calcedLastFrame++;
		    }
            */

            // ^^ this seems like the final setting
        }
    }
}
