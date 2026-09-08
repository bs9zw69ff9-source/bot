using PavlovBot.Core.Events;
using PavlovBot.Host.Discord;
using Xunit;

namespace PavlovBot.Tests;

/// <summary>
/// Where a roster change is recorded, and which channel it reaches.
/// </summary>
/// <remarks>
/// IT WAS RECORDED NOWHERE A HUMAN READS. /whitelist, /promotion and /demotion wrote a line
/// to the application log and stopped: no audit record, so nothing in the staff channels and
/// nothing in the timeline. The only trace was the command's own public reply in whatever
/// channel it was run in - which is not a log, and which stopped existing the moment those
/// replies were made ephemeral.
///
/// EventMapping had categorised whitelist-add, whitelist-remove, promotion and demotion as
/// Faction events all along, waiting for calls nobody had written. These pin the names it
/// already expects, so the two halves cannot drift apart again.
/// </remarks>
public class RosterAuditTests
{
    [Theory]
    [InlineData("whitelist-add")]
    [InlineData("whitelist-remove")]
    [InlineData("whitelist-wipe")]
    [InlineData("promotion")]
    [InlineData("demotion")]
    [InlineData("subclass")]
    public void ARosterActionIsAFactionEvent(string action)
    {
        Assert.Equal(EventCategory.Faction, EventMapping.CategoryOf(action));
    }

    [Fact]
    public void TheWipesOldSpacedNameStillCategorisesTheSameWay()
    {
        /* It recorded itself as "whitelist wipe" before it was hyphenated to match the rest.
           Records already in the timeline carry that string, and dropping it would move
           every historical wipe into a different category on the next read. */
        Assert.Equal(EventCategory.Faction, EventMapping.CategoryOf("whitelist wipe"));
    }

    [Theory]
    [InlineData("whitelist-add")]
    [InlineData("whitelist-remove")]
    [InlineData("whitelist-wipe")]
    public void AWhitelistChangeGoesToTheModLogChannel(string action)
    {
        /* Not a ban and not police, so it falls through to MOD_LOG_CHANNEL - which is where
           an operator looking for "who added whom" will go. */
        var channels = new StaffLogChannels(Mod: 100, Ban: 200, Police: 300, Arrest: 400);

        Assert.Equal(100UL, ChannelStaffLog.ChannelFor(action, channels));
    }

    [Theory]
    [InlineData("promotion")]
    [InlineData("demotion")]
    [InlineData("subclass")]
    public void ARankChangeGoesToThePoliceLogChannelInstead(string action)
    {
        /* NOT the same channel as the whitelist changes, which is worth pinning because it
           is surprising: PoliceActions already listed these alongside arrest and warrant,
           and that classification predates them being audited at all. So turning the
           recording on routes them somewhere different from /whitelist without anybody
           choosing it here - stated in a test rather than discovered in a channel. */
        var channels = new StaffLogChannels(Mod: 100, Ban: 200, Police: 300, Arrest: 400);

        Assert.Equal(300UL, ChannelStaffLog.ChannelFor(action, channels));
    }

    [Fact]
    public void WithOnlyABanChannelSetItStillLandsSomewhere()
    {
        /* The fallback that matters: an operator who set one channel meant "everything
           here", and a roster change vanishing because its own channel was unset is the
           silent drop this routing exists to avoid. */
        var channels = new StaffLogChannels(Mod: null, Ban: 200, Police: null, Arrest: null);

        Assert.Equal(200UL, ChannelStaffLog.ChannelFor("whitelist-add", channels));
    }

    [Fact]
    public void WithNoChannelsConfiguredItGoesNowhereRatherThanThrowing()
    {
        // Perfectly normal: the audit record is still written to the store either way.
        var channels = new StaffLogChannels(Mod: null, Ban: null, Police: null, Arrest: null);

        Assert.Null(ChannelStaffLog.ChannelFor("whitelist-add", channels));
    }
}
