namespace PavlovBot.Host.Storage;

/// <summary>
/// Every dataset the bot persists, and what an empty one looks like.
/// </summary>
/// <remarks>
/// Names match the Node bot's file names exactly (minus the <c>./</c> and <c>.json</c>), so
/// the two share one <c>bot.db</c> during the migration. A renamed dataset here would read
/// as empty rather than failing - a bot that silently forgets every ban - so the names are
/// not a detail to tidy up.
///
/// The SEED matters as much as the name: a dataset whose natural empty value is <c>[]</c>
/// but which is seeded <c>{}</c> deserialises to the fallback on every read, which looks
/// like it works right up until a write turns the wrong shape into the stored one.
/// </remarks>
public static class Datasets
{
    // ---- moderation ----
    public const string TempBans = "tempbans";
    public const string ModLog = "modlog";
    public const string Mutes = "mutes";
    /// <summary>
    /// Discord user ids barred from every command. AN ARRAY OF STRINGS - the Node bot's
    /// format, and it reads this exact dataset (<c>index.js</c>, <c>BLACKLIST_IDS</c>).
    /// </summary>
    public const string UserBlacklist = "user_blacklist";
    public const string UserUnbarred = "user_unbarred";

    /// <summary>
    /// IP, name and account-id flags for ban-evasion matching.
    /// </summary>
    /// <remarks>
    /// SEPARATE FROM <see cref="UserBlacklist"/>, and it has to be. Both bots share one
    /// database, and this data used to live under that name in C# while the Node bot used
    /// the same name for its array of barred Discord ids. Each bot's write destroyed the
    /// other's data: C# writing an object left Node calling <c>.map</c> on it, and Node
    /// writing an array left C# reading no flags at all - which silently turns off
    /// ban-evasion auto-banning, the failure that announces itself least.
    /// </remarks>
    public const string IpFlags = "ip_flags";

    /// <summary>
    /// In-game names whose addresses are never recorded. An array of strings.
    /// </summary>
    /// <remarks>
    /// Persisted rather than in-memory: an ignore list that empties on restart is worse
    /// than none, because it silently starts collecting the addresses of the accounts
    /// somebody deliberately asked it not to.
    /// </remarks>
    public const string IgnoredNames = "ignored_names";
    public const string AutobanExempt = "autoban_exempt";

    /// <summary>
    /// Players no automated path may ever ban. Name -> when it was set.
    /// </summary>
    /// <remarks>
    /// SEPARATE FROM MASTER_NAMES, deliberately. A master name is an owner's own account and
    /// carries privileges elsewhere; this carries none. It is the answer to "this player is
    /// being auto-banned and I cannot work out why" - an owner should be able to stop it in
    /// one command, from Discord, without a restart and without granting anybody anything.
    ///
    /// It does NOT stop a human banning them. A moderator typing /ban still works, which is
    /// the point: this turns off the machine, not the staff.
    /// </remarks>
    public const string NeverBan = "never_ban";

    /// <summary>
    /// The id RCON uses for a player -> the display name last seen against it.
    /// </summary>
    /// <remarks>
    /// LEARNED FROM THE ROSTER, because nothing else in the bot holds this pairing. The
    /// evasion registry is keyed on the EOS id that Pavlov.log writes - a 32-character hex
    /// string - and Stats.log records the id RCON targets, which on a Shack server is a plain
    /// number. Two identifier spaces that never meet, so a kill read out of Stats.log had
    /// nothing to resolve against except a roster that happened to be loaded at that instant.
    ///
    /// Persisted so a name survives the player leaving, the roster going stale, and a
    /// restart. Seen once is enough.
    /// </remarks>
    public const string PlayerNames = "player_names";

    /// <summary>Player -> their warnings, newest last. Escalation counts these.</summary>
    public const string Warnings = "warnings";

    public const string BanReconcileState = "ban_reconcile_state";

    /// <summary>
    /// Players deliberately unbanned, and when. Name -> lift instant.
    /// </summary>
    /// <remarks>
    /// THE GUARD AGAINST A LIFT BEING UNDONE BY THE NEXT SYNC. ServerBanFile imports the
    /// game's own ban file and treats anything not already in the store as a ban to create.
    /// An unban removes the store record, but the FILE still lists them until the next export
    /// - so the import five minutes later saw a name it did not recognise and re-created the
    /// ban it had just been told to lift. The export then wrote it back, and the sweep
    /// enforced it. Bans came back on their own, minutes after being lifted.
    ///
    /// A tombstone is the backstop rather than the fix - the lift also rewrites the file now -
    /// but it is the half that survives an export that fails, a path that is misconfigured,
    /// or a file somebody edits by hand.
    /// </remarks>
    public const string UnbanTombstones = "unban_tombstones";
    public const string VpnChecks = "vpn_checks";
    public const string ServerLock = "server_lock";

    // ---- factions ----
    public const string FactionRanks = "faction_ranks";
    public const string FactionConfig = "faction_config";
    public const string FactionAudit = "faction_audit";
    public const string FactionBackup = "faction_backup";

    // ---- police ----
    public const string Warrants = "warrants";
    public const string Arrests = "arrests";
    public const string Sentences = "sentences";
    public const string PoliceConfig = "police_config";
    public const string RankSuspensions = "rank_suspensions";

    // ---- players ----
    public const string Playtime = "playtime";
    public const string LastSeen = "lastseen";
    public const string KnownPlayers = "known_players";
    public const string DiscordLinks = "discord_links";
    public const string Verifications = "verifications";

    /// <summary>Discord id -> the faction and in-game name they were whitelisted under.</summary>
    /// <remarks>
    /// New in the C# bot; the Node bot had no equivalent, so there is no file shape to match.
    /// An INDEX over the roster files rather than a second source of truth - see FactionMembers.
    /// </remarks>
    public const string FactionMembers = "faction_members";
    public const string VerifyState = "verify_state";
    public const string DonatorSuspend = "donator_suspend";

    /// <summary>
    /// Payroll runs, newest last. An ARRAY - the Node bot's shape, and this name is its own.
    /// </summary>
    public const string Wages = "wages";

    /// <summary>
    /// Rolling per-player earnings, for anomaly detection. Player -> recent credits.
    /// </summary>
    /// <remarks>
    /// PERSISTED rather than in-memory, because the window that matters is longer than an
    /// uptime. A detector that forgets everything on restart is one a deploy silently
    /// disarms, and the money exploit worth catching is the slow one.
    /// </remarks>
    public const string MoneyWindow = "money_window";

    /// <summary>Last payroll instant per faction, so a restart cannot re-pay a period.</summary>
    public const string PayrollState = "payroll_state";

    // ---- menus ----
    public const string MenuGrants = "menu_grants";
    public const string MenuPanel = "menu_panel";
    public const string MenuRoles = "menu_roles";
    public const string MenuLinks = "menu_links";

    // ---- bot config and surfaces ----
    public const string Roles = "roles";
    public const string AutoRotate = "autorotate";
    public const string ServerStats = "server_stats";
    public const string AutopostState = "autopost_state";
    public const string UpdateLogState = "update_log_state";

    /// <summary>Dataset -> the JSON an empty one contains.</summary>
    public static readonly IReadOnlyDictionary<string, string> Seeds = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [TempBans] = "[]",
        [ModLog] = "[]",
        [Mutes] = "{}",
        [UserBlacklist] = "[]",
        [UserUnbarred] = "[]",
        [IpFlags] = "{}",
        [IgnoredNames] = "[]",
        [AutobanExempt] = "{}",
        [NeverBan] = "{}",
        [PlayerNames] = "{}",
        [Warnings] = "{}",
        [BanReconcileState] = "{}",
        [UnbanTombstones] = "{}",
        [VpnChecks] = "{}",
        [ServerLock] = "{}",

        [FactionRanks] = "{}",
        [FactionConfig] = "{}",
        [FactionAudit] = "[]",
        [FactionBackup] = "{}",

        [Warrants] = "{}",
        [Arrests] = "{}",
        [Sentences] = "{}",
        [PoliceConfig] = """{"bailRate":1}""",
        [RankSuspensions] = "{}",

        [Playtime] = "{}",
        [LastSeen] = "{}",
        [KnownPlayers] = "{}",
        [DiscordLinks] = "{}",
        [Verifications] = "{}",
        [FactionMembers] = "{}",
        [VerifyState] = "{}",
        [DonatorSuspend] = "{}",
        [Wages] = "[]",
        [MoneyWindow] = "{}",
        [PayrollState] = "{}",

        [MenuGrants] = "{}",
        [MenuPanel] = "{}",
        [MenuRoles] = "{}",
        [MenuLinks] = "{}",

        [Roles] = """{"modRoleId":"","adminRoleId":"","factionLeaderRoleId":"","policeRoleId":"","gambinoRoleId":"","colomboRoleId":"","nypdRoleId":""}""",
        [AutoRotate] = "{}",
        [ServerStats] = "{}",
        [AutopostState] = "{}",
        [UpdateLogState] = "{}",
    };

    public static IReadOnlyCollection<string> All { get; } = Seeds.Keys.ToList();
}
