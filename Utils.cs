using BepInEx.Logging;
using Comfort.Common;
using EFT;
using Object = UnityEngine.Object;


namespace ombarella
{
    public static class Utils
    {
        static float _logUpdateTimer = 0;
        public static ManualLogSource Logger;

        public static Player GetMainPlayer()
        {
            GameWorld instance = Singleton<GameWorld>.Instance;
            if ((Object)(object)instance == (Object)null)
            {
                return null;
            }
            return instance.MainPlayer;
        }

        public static void Log(string log, bool oneTimeLog)
        {
            if (oneTimeLog)
            {
                Logger.LogInfo((object)log);
            }
            else
            {
                float limit = 1f / Plugin.SamplesPerSecond.Value;
                if (_logUpdateTimer > limit)
                {
                    Logger.LogInfo((object)log);
                }
            }
        }

        public static void LogError(string error)
        {
            Logger.LogError((object)error);
        }

        public static void UpdateDebug(float dt)
        {
            _logUpdateTimer += dt;
            float limit = 1f / Plugin.SamplesPerSecond.Value;
            if (_logUpdateTimer > limit)
            {
                _logUpdateTimer = _logUpdateTimer - Plugin.DebugUpdateFreq.Value;
            }
        }

        public static bool IsInRaid()
        {
            return GClass2107.InRaid;
        }
    }
}
