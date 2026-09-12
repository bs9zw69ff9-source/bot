using Microsoft.Extensions.Logging.Abstractions;
using PavlovBot.Core.Data;
using PavlovBot.Host.Logs;
using PavlovBot.Host.Observability;
using PavlovBot.Host.Storage;
using PavlovBot.Core.Logs;
using Xunit;

namespace PavlovBot.Tests;

/// <summary>
/// Pavlov's third identifier for a player.
/// </summary>
/// <remarks>
/// THREE IDS FOR ONE PERSON, none of which looks like the others:
///
///   the display name       sweet_tea_is_good
///   the EOS id             0002913af4f445df86da1be6a2a01728
///   the platform id        7141175386003511
///
/// The bot understood the first two. The third is what Stats.log records against a kill and
/// what a moderator ends up pasting into a command having found it somewhere with no name
/// attached - and every command answered "no record of this player at all", which is the one
/// answer that is certainly wrong.
///
/// The lines below are verbatim from a live server, which is the only reason the correlation
/// is safe to rely on: the platform id arrives ALONE and the account that owns it is named
/// three lines later.
/// </remarks>
public class PlatformIdTests
{
    private const string PlatformLine =
        "[2026.09.12-11.04.54:787][562]PavlovLog: Player login with platformid 7141175386003511";

    private const string JoinedLine =
        "[2026.09.12-11.04.54:787][562]PavlovLog: Player 0002913af4f445df86da1be6a2a01728 Joined, current bot Num = 0";

    [Fact]
    public void ThePlatformIdIsReadOffItsOwnLine()
    {
        Assert.Equal("7141175386003511", PavlovLog.PlatformId(PlatformLine));
    }

    [Fact]
    public void TheJoinedLineNamesTheAccountThatOwnsIt()
    {
        Assert.Equal("0002913af4f445df86da1be6a2a01728", PavlovLog.JoinedAccount(JoinedLine));
    }

    [Fact]
    public void NeitherPatternMatchesTheOthersLine()
    {
        /* THE CORRELATION DEPENDS ON THIS. Both lines start "PavlovLog: Player", and if
           either pattern caught the other the pairing would attach a platform id to itself
           or discard it before the account arrived. */
        Assert.Null(PavlovLog.PlatformId(JoinedLine));
        Assert.Null(PavlovLog.JoinedAccount(PlatformLine));
    }

    [Fact]
    public void TheLoginRequestLineIsNotMistakenForAJoin()
    {
        /* The login line carries the EOS id too, but it arrives BEFORE the platform id with
           other lines in between - so matching it would pair the id with the previous
           player. Only the Joined line, which follows immediately, is used. */
        const string login =
            "[2026.09.12-11.04.52:928][451]LogNet: Login request: ?Name=sweet_tea_is_good?platform=oculus " +
            "userId: NULL:0002913af4f445df86da1be6a2a01728 platform: NULL";

        Assert.Null(PavlovLog.JoinedAccount(login));
    }

    [Fact]
    public void AnOrdinaryLineYieldsNothing()
    {
        Assert.Null(PavlovLog.PlatformId("[..]PavlovLog: PostLogin"));
        Assert.Null(PavlovLog.JoinedAccount("[..]PavlovLog: Authenticating player sweet_tea_is_good"));
    }
}

/// <summary>
/// The platform id reaching the account, through the real ingest.
/// </summary>
/// <remarks>
/// The lines are verbatim from a live server, in the order the server wrote them. That order
/// is the whole correlation: the platform id arrives ALONE and the account it belongs to is
/// named three lines later, so a parser that only ever sees one line at a time has to hold it.
/// </remarks>
public class PlatformIdIngestTests
{
    private const string File1 = "/server1/Pavlov.log";
    private const string Eos = "0002913af4f445df86da1be6a2a01728";
    private const string Platform = "7141175386003511";

    private static IpTrackingService Service() =>
        new(new SerializedStore(new MemoryBackend(), new SystemTextJsonCodec()),
            new MetricsRegistry(), NullLogger<IpTrackingService>.Instance);

    private static async Task JoinAsync(IpTrackingService service, string file, string name, string eos, string platform)
    {
        await service.IngestAsync(new LogLine(file,
            $"[2026.09.12-11.04.52:928][451]LogNet: Login request: ?Name={name}?platform=oculus userId: NULL:{eos} platform: NULL"));
        await service.IngestAsync(new LogLine(file,
            $"[2026.09.12-11.04.54:787][562]PavlovLog: Player login with platformid {platform}"));
        await service.IngestAsync(new LogLine(file,
            $"[2026.09.12-11.04.54:787][562]PavlovLog: Authenticating player {name}"));
        await service.IngestAsync(new LogLine(file,
            $"[2026.09.12-11.04.54:787][562]PavlovLog: Player {eos} Joined, current bot Num = 0"));
    }

    [Fact]
    public async Task AJoinRecordsThePlatformIdAgainstTheAccount()
    {
        var service = Service();

        await JoinAsync(service, File1, "sweet_tea_is_good", Eos, Platform);

        Assert.Equal(Platform, service.Account(Eos)!.PlatformId);
    }

    [Fact]
    public async Task APlatformIdResolvesToThePlayer()
    {
        /* WHAT THIS IS FOR. Somebody holds a seventeen-digit number off a kill record or a
           log line and wants to know who it is. Every command answered "no record of this
           player at all". */
        var service = Service();

        await JoinAsync(service, File1, "sweet_tea_is_good", Eos, Platform);

        Assert.Equal("sweet_tea_is_good", service.Resolve(Platform)?.Name);
        Assert.Equal("sweet_tea_is_good", service.Resolve(Eos)?.Name);
        Assert.Equal("sweet_tea_is_good", service.Resolve("sweet_tea_is_good")?.Name);
        Assert.Null(service.Resolve("24686966564314946"));
    }

    [Fact]
    public async Task TwoServersDoNotCrossTheirPendingIds()
    {
        /* Held PER FILE. Two servers write two logs and the bot reads both in one loop, so a
           platform id waiting on one server's Joined line must not be claimed by the other's
           - that attaches somebody's id to a different person entirely. */
        const string file2 = "/server2/Pavlov.log";
        const string otherEos = "0002f1640d5641c3a02da5cf31b4b39b";
        const string otherPlatform = "25421435350859341";
        var service = Service();

        await service.IngestAsync(new LogLine(File1,
            $"[..]PavlovLog: Player login with platformid {Platform}"));
        await JoinAsync(service, file2, "yondu", otherEos, otherPlatform);
        await service.IngestAsync(new LogLine(File1,
            $"[..]PavlovLog: Player {Eos} Joined, current bot Num = 0"));

        Assert.Equal(otherPlatform, service.Account(otherEos)!.PlatformId);
        Assert.Equal(Platform, service.Account(Eos)!.PlatformId);
    }

    [Fact]
    public async Task AJoinWithNoPlatformIdLineLeavesTheAccountAlone()
    {
        // Not every join carries one, and an account without it must still be a valid record
        // rather than one waiting on a field that never arrives.
        var service = Service();

        await service.IngestAsync(new LogLine(File1,
            $"[..]LogNet: Login request: ?Name=Alice?platform=oculus userId: NULL:{Eos} platform: NULL"));
        await service.IngestAsync(new LogLine(File1, $"[..]PavlovLog: Player {Eos} Joined, current bot Num = 0"));

        Assert.Null(service.Account(Eos)!.PlatformId);
        Assert.Equal("Alice", service.Account(Eos)!.Name);
    }
}
