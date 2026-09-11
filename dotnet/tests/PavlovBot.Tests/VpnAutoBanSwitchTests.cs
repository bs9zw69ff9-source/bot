using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PavlovBot.Core.Data;
using PavlovBot.Core.Moderation;
using PavlovBot.Core.Vpn;
using PavlovBot.Host.Configuration;
using PavlovBot.Host.Discord;
using PavlovBot.Host.Moderation;
using PavlovBot.Host.Observability;
using PavlovBot.Host.Rcon;
using PavlovBot.Host.Storage;
using Xunit;

namespace PavlovBot.Tests;

/// <summary>
/// The switch that stops the bot issuing permanent bans on its own.
/// </summary>
/// <remarks>
/// WHY THIS EXISTS. VPN screening is the only thing here that bans with no human in the loop
/// and no blacklist entry anybody typed - it acts on a probabilistic verdict from third-party
/// providers, and those are wrong about residential and mobile addresses often enough that
/// the merge code carries an example of it. A false positive is a PERMANENT ban on somebody
/// who did nothing, issued seconds after they joined, and until this switch existed the only
/// way to stop it was to delete the API keys and lose the screening too.
///
/// OFF STILL SCREENS AND STILL REPORTS. That is the property worth pinning: an operator has
/// to be able to see what WOULD have been banned before deciding to let it, or turning it
/// back on is an act of faith.
/// </remarks>
public class VpnAutoBanSwitchTests : IAsyncDisposable
{
    private const string Player = "SomeVpnUser";

    private readonly FakeRconServer _server = new();
    private readonly SerializedStore _store = new(new MemoryBackend(), new SystemTextJsonCodec());

    private sealed class NoMasters : IMasterNames
    {
        public bool IsMaster(string name) => false;
        public bool IsExempt(string name) => false;
        public bool IsProtected(string name) => false;
        public Task ExemptAsync(string name, TimeSpan? duration = null, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private VpnResponder Responder(bool autoBan)
    {
        var metrics = new MetricsRegistry();
        var rcon = new RconRegistry(
            new BotOptions
            {
                DiscordToken = "t",
                Servers = [],
                Monitoring = new MonitoringOptions(null, "127.0.0.1", null),
                DataDirectory = Path.GetTempPath(),
            }, metrics, NullLogger<RconRegistry>.Instance);

        var masters = new NoMasters();
        return new VpnResponder(
            new BanService(rcon, _store, masters, NullLogger<BanService>.Instance),
            masters, _store, new AuditLog(_store),
            new FeedWebhooks(NullLogger<FeedWebhooks>.Instance, metrics),
            metrics, NullLogger<VpnResponder>.Instance, autoBan);
    }

    /// <summary>A verdict the responder would act on: flagged, and over the threshold.</summary>
    private static VpnRecord Actionable() => new()
    {
        Ip = "45.10.20.30",
        Decision = new VpnDecision(Flagged: true, Confirmed: true, Actionable: true,
            "confirmation agreed (1/1); 2 of 3 detector(s) flagged it, 2 required -> actionable"),
        ScreenHits = 2,
        ScreenAnswered = 3,
    };

    private List<BanRecord> Bans() => _store.Read<List<BanRecord>>(Datasets.TempBans, []);

    [Fact]
    public async Task WithTheSwitchOffAnActionableVerdictBansNobody()
    {
        // THE POINT. Same verdict, no consequence, and nothing written to the ban store.
        var outcome = await Responder(autoBan: false).RespondAsync(Player, Actionable());

        Assert.Equal(VpnBanOutcome.Disabled, outcome);
        Assert.Empty(Bans());
    }

    [Fact]
    public async Task WithTheSwitchOnTheSameVerdictStillBans()
    {
        /* The control, and the reason the default is ON: this is behaviour somebody asked
           for, and the switch must not quietly take it away. */
        var outcome = await Responder(autoBan: true).RespondAsync(Player, Actionable());

        Assert.Equal(VpnBanOutcome.Banned, outcome);
        var ban = Assert.Single(Bans());
        Assert.Equal(Player, ban.PlayerId);
        Assert.Equal("auto", ban.Moderator);
    }

    [Fact]
    public async Task TheSwitchIsCheckedAfterTheThresholdSoItCannotHideAWeakVerdict()
    {
        /* A verdict that was never actionable must still report as below-threshold rather
           than as "the switch is off" - those are different facts, and an operator deciding
           whether to turn banning on needs the real one. */
        var weak = Actionable() with
        {
            Decision = new VpnDecision(Flagged: true, Confirmed: null, Actionable: false,
                "1 of 3 detector(s) flagged it, 2 required -> NOT actionable"),
        };

        Assert.Equal(VpnBanOutcome.BelowThreshold,
            await Responder(autoBan: false).RespondAsync(Player, weak));
    }

    [Fact]
    public async Task ACleanVerdictIsStillNothingToDoEitherWay()
    {
        var clean = Actionable() with
        {
            Decision = new VpnDecision(false, null, false, "all regular checks clean"),
        };

        Assert.Equal(VpnBanOutcome.NoVerdict, await Responder(autoBan: false).RespondAsync(Player, clean));
        Assert.Equal(VpnBanOutcome.NoVerdict, await Responder(autoBan: true).RespondAsync(Player, clean));
    }

    [Fact]
    public void TheDefaultIsOnAndAnExplicitZeroTurnsItOff()
    {
        /* Unset must not change behaviour for a deployment that has this working, and the
           documented values have to be the ones that actually parse. */
        static bool Read(string? value) => FeatureOptions.Bind(
            new ConfigurationBuilder()
                .AddInMemoryCollection(value is null
                    ? []
                    : [new KeyValuePair<string, string?>("VPN_AUTOBAN", value)])
                .Build()).VpnAutoBan;

        Assert.True(Read(null));
        Assert.True(Read("1"));
        Assert.True(Read("true"));
        Assert.False(Read("0"));
        Assert.False(Read("false"));
        Assert.False(Read("off"));
    }

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        await _server.DisposeAsync();
    }
}
