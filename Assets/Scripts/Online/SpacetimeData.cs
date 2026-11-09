using System;
using UnityEngine;

namespace SeasonRush
{
    /// <summary>
    /// Where the SpacetimeDB database lives. The setup tool creates the asset
    /// Assets/Resources/SpacetimeConfig.asset: fill in <see cref="database"/> after publishing the module
    /// (see docs/SpacetimeDB_Setup.md). Leave it empty to play without the database.
    /// Command line overrides: -sdbUri http://127.0.0.1:3000  -sdbDb my-database
    /// </summary>
    [CreateAssetMenu(menuName = "Season Rush/SpacetimeDB Config", fileName = "SpacetimeConfig")]
    public class SpacetimeConfig : ScriptableObject
    {
        [Tooltip("Server address. Maincloud is https://maincloud.spacetimedb.com. A local server is http://127.0.0.1:3000")]
        public string uri = "https://maincloud.spacetimedb.com";

        [Tooltip("Name of the published database (the name you gave to 'spacetime publish'). Empty = database features off.")]
        public string database = "";
    }

    public static class SpacetimeSettings
    {
        public static string Uri { get; private set; } = "https://maincloud.spacetimedb.com";
        public static string Database { get; private set; } = "";

        /// <summary>True when a database name is configured. Without it the game works exactly as before.</summary>
        public static bool Enabled => !string.IsNullOrWhiteSpace(Database);

        static SpacetimeSettings() => Reload();

        /// <summary>
        /// Reads SpacetimeConfig.asset again every time the game starts. Without this, an Editor that has "Reload Domain" switched off
        /// (Project Settings > Editor > Enter Play Mode Settings) keeps the database name it read the first time, so a
        /// newly published database would not be used until Unity is restarted.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Reload()
        {
            Uri = "https://maincloud.spacetimedb.com";
            Database = "";
            var config = Resources.Load<SpacetimeConfig>("SpacetimeConfig");
            if (config != null)
            {
                if (!string.IsNullOrWhiteSpace(config.uri)) Uri = config.uri.Trim();
                if (!string.IsNullOrWhiteSpace(config.database)) Database = config.database.Trim();
            }
            if (GameConfig.TryGetArg("-sdbUri", out string uri) && !string.IsNullOrWhiteSpace(uri)) Uri = uri.Trim();
            if (GameConfig.TryGetArg("-sdbDb", out string db) && !string.IsNullOrWhiteSpace(db)) Database = db.Trim();
        }
    }

    /// <summary>
    /// The player's display name on this computer. A name typed in the menu is remembered; without one every
    /// launch gets a throw-away guest name (so two windows on one PC are two different players).
    /// </summary>
    public static class LocalPlayer
    {
        const string Key = "sr_name";
        public const int MaxLength = 16;
        static string _guest;

        /// <summary>A name from the command line (-name). Used for this run only and never saved.</summary>
        public static string Override { get; set; } = "";

        public static bool HasSavedName => !string.IsNullOrWhiteSpace(PlayerPrefs.GetString(Key, ""));

        public static string Name
        {
            get
            {
                string forced = Clean(Override);
                if (forced.Length > 0) return forced;
                string saved = Clean(PlayerPrefs.GetString(Key, ""));
                if (saved.Length > 0) return saved;
                return _guest ??= "Guest" + UnityEngine.Random.Range(1000, 10000);
            }
        }

        public static void Save(string name)
        {
            PlayerPrefs.SetString(Key, Clean(name));
            PlayerPrefs.Save();
        }

        /// <summary>Trims, keeps letters, digits, space, '_' and '-', and limits the length (same rule as the database).</summary>
        public static string Clean(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            var sb = new System.Text.StringBuilder();
            foreach (char c in name.Trim())
                if (char.IsLetterOrDigit(c) || c == ' ' || c == '_' || c == '-') sb.Append(c);
            string result = sb.ToString().Trim();
            return result.Length > MaxLength ? result.Substring(0, MaxLength).Trim() : result;
        }
    }

    // --------------------------------------------------------------------------------------------
    // Plain data objects. Only SpacetimeManager touches the generated SpacetimeDB types; the rest of the
    // game (menu, HUD, matchmaking) works with these simple copies.
    // --------------------------------------------------------------------------------------------

    public sealed class ProfileInfo
    {
        public string IdHex = "";
        public string Name = "";
        public bool Online;
        public uint Matches, Wins, Losses, Draws, Kills, Deaths;
        public ulong Score;
    }

    public sealed class MatchInfo
    {
        public ulong Id;
        public string Code = "";
        public string BlueName = "", RedName = "";
        public int BlueScore, RedScore;
        /// <summary>0 = blue won, 1 = red won, -1 = draw.</summary>
        public int Winner;
        public DateTime PlayedAtUtc;
    }

    public sealed class PairingInfo
    {
        public ulong Id;
        public string Code = "";
        public bool IAmHost;
        public string HostName = "", GuestName = "";
        public string HostIdHex = "", GuestIdHex = "";
        /// <summary>Empty until the host is ready. "relay" or "ip:port".</summary>
        public string HostAddress = "";
        public string OpponentName => IAmHost ? GuestName : HostName;
    }
}
