using BepInEx.Logging;
using Comfort.Common;
using EFT;
using EFT.UI;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;


namespace ombarella
{
    public static class Utils
    {
        static int alternatePlayerID = 0;
        static bool isAlternatePlayerID = false;
        static float _logUpdateTimer = 0;
        static readonly Dictionary<Type, FieldInfo> _isObservedAIFieldCache = new Dictionary<Type, FieldInfo>();
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
                return new List<Player>();
            }
            return instance.AllAlivePlayersList ?? new List<Player>();
        }

        public static List<Player> GetActualHumanPlayers(List<Player> players)
        {
            List<Player> result = new List<Player>();
            if (players == null)
            {
                return result;
            }

            foreach (Player player in players)
            {
                if (IsActualHumanPlayer(player))
                {
                    result.Add(player);
                }
            }

            return result;
        }

        public static List<Player> GetBotPlayers(List<Player> players)
        {
            List<Player> result = new List<Player>();
            if (players == null)
            {
                return result;
            }

            foreach (Player player in players)
            {
                if (IsLightMeterUsablePlayer(player) && IsBotPlayer(player))
                {
                    result.Add(player);
                }
            }

            return result;
        }

        public static bool IsActualHumanPlayer(Player player)
        {
            return IsLightMeterUsablePlayer(player) && !IsBotPlayer(player) && !IsHeadlessPlayer(player);
        }

        public static bool IsBotPlayer(Player player)
        {
            if ((Object)(object)player == (Object)null)
            {
                return false;
            }

            if (player.IsAI)
            {
                return true;
            }

            return IsFikaObservedAI(player);
        }

        public static bool IsHeadlessPlayer(Player player)
        {
            if ((Object)(object)player == (Object)null || player.Profile == null || player.Profile.Info == null)
            {
                return false;
            }

            if (StringStartsWith(player.Profile.Info.Nickname, "headless_"))
            {
                return true;
            }

            return string.Equals(player.Profile.Info.GroupId, "HEADLESS", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsLightMeterUsablePlayer(Player player)
        {
            if ((Object)(object)player == (Object)null)
            {
                return false;
            }

            if (player.HealthController == null || !player.HealthController.IsAlive)
            {
                return false;
            }

            if (player.PlayerBones == null || player.PlayerBones.Head == null || player.PlayerBones.Ribcage == null)
            {
                return false;
            }

            return IsFinite(player.Position) && IsFinite(player.PlayerBones.Head.position) && IsFinite(player.PlayerBones.Ribcage.position);
        }

        static bool IsFikaObservedAI(Player player)
        {
            FieldInfo field = GetIsObservedAIField(player.GetType());
            if (field == null || field.FieldType != typeof(bool))
            {
                return false;
            }

            return (bool)field.GetValue(player);
        }

        static FieldInfo GetIsObservedAIField(Type type)
        {
            if (type == null)
            {
                return null;
            }

            FieldInfo field;
            if (_isObservedAIFieldCache.TryGetValue(type, out field))
            {
                return field;
            }

            Type currentType = type;
            while (currentType != null)
            {
                field = currentType.GetField("IsObservedAI", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    break;
                }
                currentType = currentType.BaseType;
            }

            _isObservedAIFieldCache[type] = field;
            return field;
        }

        public static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        public static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        static bool StringStartsWith(string value, string prefix)
        {
            return !string.IsNullOrEmpty(value) && value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
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

        public static bool IsInRaid()
        {
            return Singleton<AbstractGame>.Instantiated && Singleton<AbstractGame>.Instance != null && Singleton<AbstractGame>.Instance.InRaid;
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
