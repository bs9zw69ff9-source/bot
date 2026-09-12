using PavlovBot.Core.Data;
using PavlovBot.Host.Discord;
using PavlovBot.Host.Storage;
using Xunit;

namespace PavlovBot.Tests;

/// <summary>
/// A role mapping surviving a restart.
/// </summary>
/// <remarks>
/// /setroles reports the value it just wrote IN MEMORY - UpdateAsync returns the mutated
/// object, not a re-read - so "Updated" says nothing about whether the map can be read back.
/// Every role check goes through Access.Roles, which reads it from the store on every call.
/// If the round trip loses anything, the command reports success and the role grants nothing,
/// which is exactly the shape of bug that has to be tested rather than reasoned about.
/// </remarks>
public class RoleMapRoundTripTests
{
    private static SerializedStore Store() => new(new MemoryBackend(), new SystemTextJsonCodec());

    [Fact]
    public async Task EveryTierSurvivesTheStore()
    {
        var store = Store();

        await store.UpdateAsync(Datasets.Roles, RoleMap.Empty, _ => new RoleMap
        {
            ModRole = 1234567890123456789,
            AdminRole = 987654321098765432,
            FactionLeaderRole = 111111111111111111,
            PoliceRole = 222222222222222222,
        });

        var read = store.Read(Datasets.Roles, RoleMap.Empty);

        Assert.Equal(1234567890123456789ul, read.ModRole);
        Assert.Equal(987654321098765432ul, read.AdminRole);
        Assert.Equal(111111111111111111ul, read.FactionLeaderRole);
        Assert.Equal(222222222222222222ul, read.PoliceRole);
    }

    [Fact]
    public async Task PerFactionRolesSurviveTheStore()
    {
        /* THE ONE MOST LIKELY TO BREAK. FactionRoles is an IReadOnlyDictionary behind an
           init-only property, which is a shape System.Text.Json has not always been willing
           to construct - and a deserialisation that throws hands back the FALLBACK, so every
           role in the map reads as unset at once. */
        var store = Store();

        await store.UpdateAsync(Datasets.Roles, RoleMap.Empty, _ => new RoleMap
        {
            ModRole = 1234567890123456789,
            FactionRoles = new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase)
            {
                ["NCR"] = 555555555555555555,
                ["Legion"] = 666666666666666666,
            },
        });

        var read = store.Read(Datasets.Roles, RoleMap.Empty);

        Assert.Equal(1234567890123456789ul, read.ModRole);
        Assert.Equal(555555555555555555ul, read.RoleFor("NCR"));
        Assert.Equal(666666666666666666ul, read.RoleFor("Legion"));
    }

    [Fact]
    public async Task APerFactionRoleIsFoundWhateverTheCasing()
    {
        // A dictionary's comparer is not serialised, so the map comes back ordinal. RoleFor
        // scans for exactly this reason; this is what says the scan still works after a trip
        // through the store rather than only in memory.
        var store = Store();

        await store.UpdateAsync(Datasets.Roles, RoleMap.Empty, _ => new RoleMap
        {
            FactionRoles = new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase) { ["NCR"] = 555555555555555555 },
        });

        var read = store.Read(Datasets.Roles, RoleMap.Empty);

        Assert.Equal(555555555555555555ul, read.RoleFor("ncr"));
        Assert.Equal(555555555555555555ul, read.RoleFor("NCR"));
    }

    [Fact]
    public async Task SettingOneTierDoesNotClearTheOthersAcrossARestart()
    {
        // Two separate /setroles runs, with a read between them - which is what a restart
        // does to the merge logic in the command.
        var store = Store();

        await store.UpdateAsync(Datasets.Roles, RoleMap.Empty,
            current => current with { ModRole = 1234567890123456789 });

        var between = store.Read(Datasets.Roles, RoleMap.Empty);
        await store.UpdateAsync(Datasets.Roles, RoleMap.Empty,
            _ => between with { AdminRole = 987654321098765432 });

        var read = store.Read(Datasets.Roles, RoleMap.Empty);

        Assert.Equal(1234567890123456789ul, read.ModRole);
        Assert.Equal(987654321098765432ul, read.AdminRole);
    }
}

/// <summary>
/// Telling "you do not have the role" apart from "your roles could not be read".
/// </summary>
/// <remarks>
/// Has() collapses three situations into one false: no role mapped, no member the bot can
/// see, and a member who simply lacks it. Three different fixes, indistinguishable from
/// outside - which is what makes "I set the role and nothing happened" take three rounds to
/// diagnose.
/// </remarks>
public class RoleStandingTests
{
    private const ulong RoleId = 1234567890123456789;

    private static Access Access() =>
        new(new SerializedStore(new MemoryBackend(), new SystemTextJsonCodec()), [], []);

    private sealed class Member(params ulong[] roles) : FakeUser(4242)
    {
        public IReadOnlyCollection<ulong> RoleIds { get; } = roles;
    }

    [Fact]
    public void NoRoleMappedIsItsOwnAnswer()
    {
        // Nothing can grant an unset tier, and that is not the same as the user lacking it.
        Assert.Equal(RoleStanding.Unset, Access().StandingOn(new FakeUser(1), null));
    }

    [Fact]
    public void AUserWhoIsNotAGuildMemberIsNotSimplyMissingTheRole()
    {
        /* THE ONE THAT READS AS THE MAPPING BEING BROKEN. In a DM, or a guild the bot was
           carried into, there is no member to read - so every role check answers false and
           the roles look unset however carefully they were configured. */
        Assert.Equal(RoleStanding.NoMember, Access().StandingOn(new FakeUser(1), RoleId));
    }

    [Fact]
    public void VisibleRolesIsNullWhenThereIsNoMemberToRead()
    {
        // Null and empty mean different things: nothing to read, versus read and there were
        // none - the second points at the Server Members Intent.
        Assert.Null(Access().VisibleRoles(new FakeUser(1)));
    }
}
