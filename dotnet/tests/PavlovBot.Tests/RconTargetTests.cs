using Microsoft.Extensions.Logging.Abstractions;
using PavlovBot.Core.Data;
using PavlovBot.Host.Discord;
using PavlovBot.Host.Logs;
using PavlovBot.Host.Observability;
using PavlovBot.Host.Storage;
using Xunit;

namespace PavlovBot.Tests;

/// <summary>
/// What actually goes in an RCON command that names a player.
/// </summary>
/// <remarks>
/// THE SERVER WORKS IN PLATFORM IDS. Pavlov targets a player by platform account id, and a
/// command naming anything else is ACCEPTED, answered normally, and enforces nothing - a ban
/// that reports success and leaves the player on the server. There is no error to notice.
///
/// Three identifiers exist for one person and only one of them works here:
///
///   display name    sweet_tea_is_good      what a moderator types
///   EOS id          0002913af4...          what the evasion flags are keyed on
///   platform id     7141175386003511       what the SERVER wants
///
/// Whitelist rosters and the game's ban file stay on display names - those are files the
/// game reads, not commands it executes, and the two are not interchangeable.
/// </remarks>
public class RconTargetTests
{
    private const string Eos = "0002913af4f445df86da1be6a2a01728";
    private const string Platform = "7141175386003511";
    private const string Name = "sweet_tea_is_good";

    private static IpTrackingService Tracking(out SerializedStore store)
    {
        store = new SerializedStore(new MemoryBackend(), new SystemTextJsonCodec());
        return new IpTrackingService(store, new MetricsRegistry(), NullLogger<IpTrackingService>.Instance);
    }

    private static async Task JoinAsync(IpTrackingService service)
    {
        const string file = "/server1/Pavlov.log";
        await service.IngestAsync(new LogLine(file,
            $"[..]LogNet: Login request: ?Name={Name}?platform=oculus userId: NULL:{Eos} platform: NULL"));
        await service.IngestAsync(new LogLine(file, $"[..]PavlovLog: Player login with platformid {Platform}"));
        await service.IngestAsync(new LogLine(file, $"[..]PavlovLog: Player {Eos} Joined, current bot Num = 0"));
    }

    [Fact]
    public async Task ANameResolvesToThePlatformId()
    {
        var tracking = Tracking(out _);
        await JoinAsync(tracking);

        Assert.Equal(Platform, tracking.RconTargetFor(Name));
    }

    [Fact]
    public async Task AStoredEosIdResolvesToThePlatformIdRatherThanBeingSentAsIs()
    {
        /* THE BUG THIS CLOSES. Ban records carry the EOS id - UniqueId has always held it -
           so passing the stored value straight to RCON is exactly the silent no-op. It has to
           be resolved, not trusted. */
        var tracking = Tracking(out _);
        await JoinAsync(tracking);

        Assert.Equal(Platform, tracking.RconTargetFor(Eos));
    }

    [Fact]
    public async Task APlatformIdResolvesToItself()
    {
        var tracking = Tracking(out _);
        await JoinAsync(tracking);

        Assert.Equal(Platform, tracking.RconTargetFor(Platform));
    }

    [Fact]
    public async Task TheEosIdIsStillWhatTheEvasionFlagsUse()
    {
        /* THE TWO MUST NOT COLLAPSE INTO ONE. AccountIdFor feeds the flag registry, which is
           keyed on the EOS id; RconTargetFor feeds the server. Using either where the other
           belongs fails silently in both directions. */
        var tracking = Tracking(out _);
        await JoinAsync(tracking);

        Assert.Equal(Eos, tracking.AccountIdFor(Name));
        Assert.NotEqual(tracking.AccountIdFor(Name), tracking.RconTargetFor(Name));
    }

    [Fact]
    public void AnUnknownPlayerResolvesToNothingRatherThanToAGuess()
    {
        Assert.Null(Tracking(out _).RconTargetFor("nobody"));
        Assert.Null(Tracking(out _).RconTargetFor(null));
    }

    [Fact]
    public async Task AnAccountWithNoPlatformIdFallsBackToTheEosId()
    {
        /* Players recorded before the platform id was being read have no number yet. The EOS
           id is what the bot sent before any of this, so the fallback is no worse than the old
           behaviour - where null would turn every such ban into a no-op. It shrinks to nothing
           as players rejoin. */
        var tracking = Tracking(out _);
        await tracking.IngestAsync(new LogLine("/server1/Pavlov.log",
            $"[..]LogNet: Login request: ?Name=Alice?platform=oculus userId: NULL:{Eos} platform: NULL"));

        Assert.Equal(Eos, tracking.RconTargetFor("Alice"));
    }
}
