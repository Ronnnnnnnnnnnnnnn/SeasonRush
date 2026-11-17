using System;
using UnityEngine;

namespace SeasonRush
{
    public enum Season { Spring = 0, Fall = 1, Winter = 2 }

    public enum EnemyTier { Weak = 0, Medium = 1, Strong = 2 }

    public enum MatchPhase { Lobby = 0, Countdown = 1, Playing = 2, Finished = 3 }

    public static class GameConfig
    {
        public const int SeasonCount = 3;
        public const int MaxPlayers = 2;
        public static float SeasonSeconds = 60f;
        public static float CountdownSeconds = 3f;
        public static float RespawnSeconds = 5f;

        // --- Arena ---
        public const float ArenaSize = 12f;

        public const int PlayerMaxHealth = 100;
        public const float PlayerSpeed = 5.5f;
        public const int AttackDamage = 15;
        public const float AttackCooldown = 0.35f;
        public const float AttackRange = 1.8f;
        public const float AttackHalfArcDegrees = 80f;
        public const float KnockbackSpeed = 9f;
        public const float EnemyKnockbackSpeed = 6f;

        public static bool AttackShovesRival = true;

        public static bool ShowOnScreenControls = true;

        public static int MaxEnemiesAlive = 8;
        public static float EnemySpawnInterval = 1.6f;
        public const float MinSpawnDistanceFromPlayers = 4f;

        public const ushort DefaultPort = 7777;
        public const string DefaultAddress = "127.0.0.1";

        static GameConfig()
        {
            string v;
            if (TryGetArg("-seasonSeconds", out v) && float.TryParse(v, out float s)) SeasonSeconds = Mathf.Max(3f, s);
            if (TryGetArg("-countdownSeconds", out v) && float.TryParse(v, out float c)) CountdownSeconds = Mathf.Max(0f, c);
            if (TryGetArg("-respawnSeconds", out v) && float.TryParse(v, out float r)) RespawnSeconds = Mathf.Max(0f, r);
            if (HasFlag("-nocontrols")) ShowOnScreenControls = false;
        }

        public static void Touch() { }

        static int _verbose = -1;

        /// <summary>
        /// True when the game was started with -autotest (automated verification run). In a normal game
        /// this is false: no bot, no extra logging, and keyboard input only counts while the window has focus.
        /// </summary>
        public static bool TestMode
        {
            get
            {
                if (_verbose < 0) _verbose = HasFlag("-autotest") ? 1 : 0;
                return _verbose == 1;
            }
        }

        public static bool HasFlag(string flag)
        {
            foreach (string a in Environment.GetCommandLineArgs())
                if (string.Equals(a, flag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public static bool TryGetArg(string name, out string value)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    value = args[i + 1];
                    return true;
                }
            }
            value = null;
            return false;
        }

        public static Color SlotColor(int slot)
        {
            return slot == 0 ? new Color(0.25f, 0.55f, 1f) : new Color(1f, 0.32f, 0.32f);
        }

        public static string SlotName(int slot)
        {
            return slot == 0 ? "BLUE" : "RED";
        }
    }
}
