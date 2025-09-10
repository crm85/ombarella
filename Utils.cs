using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using Object = UnityEngine.Object;


namespace ombarella
{
    public static class Utils
    {
        public static Player GetMainPlayer()
        {
            GameWorld instance = Singleton<GameWorld>.Instance;
            if ((Object)(object)instance == (Object)null)
            {
                return null;
            }
            return instance.MainPlayer;
        }
    }
}
