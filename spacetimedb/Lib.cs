using SpacetimeDB;

/// <summary>
/// Season Rush database module (runs inside SpacetimeDB).
///
/// It stores what has to survive between game sessions and be shared between players:
///   Player       - one row per player: display name, online flag and lifetime stats
///   QueueEntry   - players who pressed FIND MATCH and are waiting for a rival
///   Pairing      - two players the matchmaker put together (who hosts, a match code, how to connect)
///   MatchResult  - history of finished matches
///
/// The game itself (movement, combat, enemies) is NOT in here: it stays in Netcode for GameObjects.
/// SpacetimeDB only pairs the two players and records the outcome.
/// </summary>
public static partial class Module
{
    const int MaxNameLength = 16;
    const int MaxScore = 100_000;
    const int MaxCount = 10_000;
    const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";   // no I, O, 0, 1 (easy to read aloud)

    // ------------------------------------------------------------------ tables

    [SpacetimeDB.Table(Accessor = "Player", Public = true)]
    public partial struct Player
    {
        [PrimaryKey]
        public Identity Identity;
        public string Name;
        public bool Online;
        public uint MatchesPlayed;
        public uint Wins;
        public uint Losses;
        public uint Draws;
        public ulong TotalScore;
        public uint TotalKills;
        public uint TotalDeaths;
        public Timestamp LastSeen;
    }

    [SpacetimeDB.Table(Accessor = "QueueEntry", Public = true)]
    public partial struct QueueEntry
    {
        [PrimaryKey]
        public Identity Identity;
        public string Name;
        public Timestamp JoinedAt;
    }

    [SpacetimeDB.Table(Accessor = "Pairing", Public = true)]
    public partial struct Pairing
    {
        [PrimaryKey]
        [AutoInc]
        public ulong Id;
        /// <summary>Short unique code of this match. Also used as the Unity Relay session id.</summary>
        public string MatchCode;
        /// <summary>The player who waited longest. They run the Netcode host.</summary>
        public Identity Host;
        public string HostName;
        public Identity Guest;
        public string GuestName;
        /// <summary>Empty until the host is ready. "relay" = use Unity Relay, otherwise "ip:port" for a direct connection.</summary>
        public string HostAddress;
        /// <summary>True once the result of this match has been stored.</summary>
        public bool Reported;
        public Timestamp CreatedAt;
    }

    [SpacetimeDB.Table(Accessor = "MatchResult", Public = true)]
    public partial struct MatchResult
    {
        [PrimaryKey]
        [AutoInc]
        public ulong Id;
        public Timestamp PlayedAt;
        public string MatchCode;
        public Identity BlueIdentity;
        public string BlueName;
        public int BlueScore;
        public int BlueKills;
        public int BlueDeaths;
        public Identity RedIdentity;
        public string RedName;
        public int RedScore;
        public int RedKills;
        public int RedDeaths;
        /// <summary>0 = blue won, 1 = red won, -1 = draw.</summary>
        public int WinnerSlot;
    }

    // ------------------------------------------------------------------ connection lifecycle

    [SpacetimeDB.Reducer(ReducerKind.ClientConnected)]
    public static void PlayerConnected(ReducerContext ctx)
    {
        if (ctx.Db.Player.Identity.Find(ctx.Sender) is { } existing)
        {
            ctx.Db.Player.Identity.Update(existing with { Online = true, LastSeen = ctx.Timestamp });
        }
        else
        {
            ctx.Db.Player.Insert(new Player
            {
                Identity = ctx.Sender,
                Name = "Player" + ctx.Rng.Next(1000, 10000),
                Online = true,
                LastSeen = ctx.Timestamp
            });
        }
    }

    [SpacetimeDB.Reducer(ReducerKind.ClientDisconnected)]
    public static void PlayerDisconnected(ReducerContext ctx)
    {
        if (ctx.Db.Player.Identity.Find(ctx.Sender) is { } player)
            ctx.Db.Player.Identity.Update(player with { Online = false, LastSeen = ctx.Timestamp });

        // someone who closes the game must not stay in the queue
        ctx.Db.QueueEntry.Identity.Delete(ctx.Sender);
    }

    // ------------------------------------------------------------------ player profile

    [SpacetimeDB.Reducer]
    public static void SetName(ReducerContext ctx, string name)
    {
        name = (name ?? "").Trim();
        if (name.Length < 1 || name.Length > MaxNameLength)
            throw new Exception($"Name must be 1-{MaxNameLength} characters.");
        foreach (char c in name)
            if (!(char.IsLetterOrDigit(c) || c == ' ' || c == '_' || c == '-'))
                throw new Exception("Name may only contain letters, digits, spaces, '_' and '-'.");

        if (ctx.Db.Player.Identity.Find(ctx.Sender) is { } player)
            ctx.Db.Player.Identity.Update(player with { Name = name, LastSeen = ctx.Timestamp });
        else
            ctx.Db.Player.Insert(new Player { Identity = ctx.Sender, Name = name, Online = true, LastSeen = ctx.Timestamp });

        // keep the name shown in the queue up to date
        if (ctx.Db.QueueEntry.Identity.Find(ctx.Sender) is { } entry)
            ctx.Db.QueueEntry.Identity.Update(entry with { Name = name });
    }

    // ------------------------------------------------------------------ matchmaking queue

    /// <summary>
    /// Press FIND MATCH. If another player is already waiting, the two are paired right away
    /// (the one who waited longest becomes the host). Otherwise the caller waits in the queue.
    /// </summary>
    [SpacetimeDB.Reducer]
    public static void JoinQueue(ReducerContext ctx)
    {
        var me = ctx.Db.Player.Identity.Find(ctx.Sender)
                 ?? throw new Exception("Unknown player. Connect first.");

        // never wait twice
        ctx.Db.QueueEntry.Identity.Delete(ctx.Sender);

        // drop pairings nobody finished long ago so the table does not grow forever
        long cutoff = ctx.Timestamp.MicrosecondsSinceUnixEpoch - 6L * 60 * 60 * 1_000_000;
        foreach (var old in ctx.Db.Pairing.Iter().ToList())
            if (old.CreatedAt.MicrosecondsSinceUnixEpoch < cutoff)
                ctx.Db.Pairing.Id.Delete(old.Id);

        // the player who has been waiting longest (and is still online)
        QueueEntry? waiting = null;
        foreach (var entry in ctx.Db.QueueEntry.Iter().OrderBy(e => e.JoinedAt.MicrosecondsSinceUnixEpoch))
        {
            if (entry.Identity == ctx.Sender) continue;
            if (ctx.Db.Player.Identity.Find(entry.Identity) is { Online: true })
            {
                waiting = entry;
                break;
            }
            ctx.Db.QueueEntry.Identity.Delete(entry.Identity);      // stale entry of an offline player
        }

        if (waiting is { } other)
        {
            ctx.Db.QueueEntry.Identity.Delete(other.Identity);
            ctx.Db.Pairing.Insert(new Pairing
            {
                Id = 0,
                MatchCode = MakeCode(ctx),
                Host = other.Identity,
                HostName = other.Name,
                Guest = ctx.Sender,
                GuestName = me.Name,
                HostAddress = "",
                Reported = false,
                CreatedAt = ctx.Timestamp
            });
        }
        else
        {
            ctx.Db.QueueEntry.Insert(new QueueEntry { Identity = ctx.Sender, Name = me.Name, JoinedAt = ctx.Timestamp });
        }
    }

    [SpacetimeDB.Reducer]
    public static void LeaveQueue(ReducerContext ctx)
    {
        ctx.Db.QueueEntry.Identity.Delete(ctx.Sender);
    }

    /// <summary>The host tells the guest how to connect: "relay", or "ip:port" for a direct connection.</summary>
    [SpacetimeDB.Reducer]
    public static void PublishHostAddress(ReducerContext ctx, ulong pairingId, string address)
    {
        var pairing = ctx.Db.Pairing.Id.Find(pairingId) ?? throw new Exception("Unknown pairing.");
        if (pairing.Host != ctx.Sender) throw new Exception("Only the host can publish the address.");
        if ((address ?? "").Length > 80) throw new Exception("Address too long.");
        ctx.Db.Pairing.Id.Update(pairing with { HostAddress = address ?? "" });
    }

    static string MakeCode(ReducerContext ctx)
    {
        var chars = new char[8];
        for (int i = 0; i < chars.Length; i++) chars[i] = CodeAlphabet[ctx.Rng.Next(0, CodeAlphabet.Length)];
        return new string(chars);
    }

    // ------------------------------------------------------------------ results

    /// <summary>
    /// Called once by the Netcode host when a match ends. Stores the result and updates both players' lifetime stats.
    /// The caller must be one of the two players, and a match found through the queue can only be stored once.
    /// </summary>
    [SpacetimeDB.Reducer]
    public static void ReportMatch(
        ReducerContext ctx, string matchCode,
        Identity blue, int blueScore, int blueKills, int blueDeaths,
        Identity red, int redScore, int redKills, int redDeaths,
        int winnerSlot)
    {
        if (ctx.Sender != blue && ctx.Sender != red) throw new Exception("Only a player of the match can report it.");
        if (blue == red) throw new Exception("A match needs two different players.");
        if (winnerSlot < -1 || winnerSlot > 1) throw new Exception("Invalid winner.");
        CheckRange(blueScore, MaxScore); CheckRange(redScore, MaxScore);
        CheckRange(blueKills, MaxCount); CheckRange(redKills, MaxCount);
        CheckRange(blueDeaths, MaxCount); CheckRange(redDeaths, MaxCount);

        var blueRow = ctx.Db.Player.Identity.Find(blue) ?? throw new Exception("Blue player is unknown.");
        var redRow = ctx.Db.Player.Identity.Find(red) ?? throw new Exception("Red player is unknown.");

        matchCode = matchCode ?? "";
        if (matchCode.Length > 0)
        {
            // a matchmade game: the pairing must exist, match these two players and not be reported yet
            Pairing? found = null;
            foreach (var p in ctx.Db.Pairing.Iter())
                if (p.MatchCode == matchCode) { found = p; break; }
            if (found is not { } pairing) throw new Exception("Unknown match code.");
            bool samePlayers = (pairing.Host == blue && pairing.Guest == red) || (pairing.Host == red && pairing.Guest == blue);
            if (!samePlayers) throw new Exception("Players do not match the pairing.");
            if (pairing.Reported) return;                       // already stored (idempotent)
            ctx.Db.Pairing.Id.Update(pairing with { Reported = true });
        }

        ctx.Db.MatchResult.Insert(new MatchResult
        {
            Id = 0,
            PlayedAt = ctx.Timestamp,
            MatchCode = matchCode,
            BlueIdentity = blue, BlueName = blueRow.Name, BlueScore = blueScore, BlueKills = blueKills, BlueDeaths = blueDeaths,
            RedIdentity = red, RedName = redRow.Name, RedScore = redScore, RedKills = redKills, RedDeaths = redDeaths,
            WinnerSlot = winnerSlot
        });

        ctx.Db.Player.Identity.Update(Apply(blueRow, blueScore, blueKills, blueDeaths, winnerSlot == 0 ? 1 : winnerSlot == 1 ? -1 : 0));
        ctx.Db.Player.Identity.Update(Apply(redRow, redScore, redKills, redDeaths, winnerSlot == 1 ? 1 : winnerSlot == 0 ? -1 : 0));
    }

    /// <summary>outcome: 1 = win, -1 = loss, 0 = draw</summary>
    static Player Apply(Player p, int score, int kills, int deaths, int outcome)
    {
        return p with
        {
            MatchesPlayed = p.MatchesPlayed + 1,
            Wins = p.Wins + (outcome == 1 ? 1u : 0u),
            Losses = p.Losses + (outcome == -1 ? 1u : 0u),
            Draws = p.Draws + (outcome == 0 ? 1u : 0u),
            TotalScore = p.TotalScore + (ulong)score,
            TotalKills = p.TotalKills + (uint)kills,
            TotalDeaths = p.TotalDeaths + (uint)deaths
        };
    }

    static void CheckRange(int value, int max)
    {
        if (value < 0 || value > max) throw new Exception("Value out of range.");
    }
}
