using BepInEx.Logging;
using Comfort.Common;
using EFT;
using UnityEngine;
using Object = UnityEngine.Object;


namespace ombarella
{
    public static class Utils
    {
        static float _logUpdateTimer = 0;
        public static ManualLogSource Logger;
        public static bool DebugViz { get; set; }

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
            if (!Plugin.IsDebug.Value)
            {
                return;
            }
            if (oneTimeLog)
            {
                Logger.LogInfo((object)log);
            }
            else
            {
                _logUpdateTimer += Time.deltaTime;
                float logUpdateInterval = 1f / Plugin.DebugUpdateFreq.Value;
                if (_logUpdateTimer > logUpdateInterval)
                {
                    _logUpdateTimer = 0;
                    Logger.LogInfo((object)log);
                }
            }
        }

        public static void LogError(string error)
        {
            Logger.LogError((object)error);
        }

        public static void Update(float dt)
        {
            //_logUpdateTimer += dt;
            //float logUpdateInterval = 1f / Plugin.DebugUpdateFreq.Value;
            //if (_logUpdateTimer > logUpdateInterval)
            //{
            //    _logUpdateTimer = _logUpdateTimer - logUpdateInterval;
            //}
        }

        public static bool IsInRaid()
        {
            return GClass2107.InRaid;
        }

        public static void DrawDebugLine(Vector3 from, Vector3 to)
        {
            Debug.DrawLine(from, to, Color.green);
        }
    }
}
