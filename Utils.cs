using BepInEx.Logging;
using Comfort.Common;
using EFT;
using EFT.UI;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;


namespace ombarella
{
    public static class Utils
    {
        static int alternatePlayerID = 0;
        static bool isAlternatePlayerID = false;
        static float _logUpdateTimer = 0;
        public static ManualLogSource Logger;
        public static bool DebugViz { get; set; }

        public static Player GetMainPlayer()
        {
            if (isAlternatePlayerID)
            {
                return GetPlayer(alternatePlayerID);
            }
            else
            {
                GameWorld instance = Singleton<GameWorld>.Instance;
                if ((Object)(object)instance == (Object)null)
                {
                    return null;
                }
                return instance.MainPlayer;
            }
        }

        public static bool GetMainCameraCullingIndex(ref int index)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                index = mainCam.cullingMask;
                return true;
            }
            return false;
        }

        public static int GetPlayerCullingMask()
        {
            return LayerMask.NameToLayer("Player");
        }

        public static List<Player> GetAllPlayers()
        {
            GameWorld instance = Singleton<GameWorld>.Instance;
            if ((Object)(object)instance == (Object)null)
            {
                return null;
            }
            return instance.AllAlivePlayersList;
        }

        public static Player GetPlayer(int playerID)
        {
            GameWorld instance = Singleton<GameWorld>.Instance;
            if ((Object)(object)instance == (Object)null)
            {
                return null;
            }

            Player player = null;
            if (instance.TryGetAlivePlayer(playerID, out player))
            {
                return player;
            }
            else return null;
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
            return GClass2340.InRaid;
        }

        public static void DrawDebugLine(Vector3 from, Vector3 to)
        {
            Debug.DrawLine(from, to, Color.green);
        }

        public static void ForceTruePlayerID(bool setForcedPlayer, int playerID)
        {
            // this is for fika
            isAlternatePlayerID = setForcedPlayer;
            alternatePlayerID = playerID;
        }

        public static void AssignPlayerToLayer(string layerName)
        {
            Player player = GetMainPlayer();
            GameObject obj = player.gameObject;
            if (LayerMask.NameToLayer(layerName) != -1)
            {
                obj.layer = LayerMask.NameToLayer(layerName);
            }
            else
            {
                Utils.LogError($"Layer {layerName} does not exist.");
            }
        }

        public static List<string> GetExistingLayers()
        {
            List<string> layers = new List<string>();

            for (int i = 0; i < 32; i++) // Unity supports layers 0 to 31
            {
                string layerName = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(layerName))
                {
                    layers.Add(layerName);
                }
            }

            return layers;
        }
    }
}
