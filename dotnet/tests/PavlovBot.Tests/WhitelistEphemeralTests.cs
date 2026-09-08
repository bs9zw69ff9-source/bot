using Microsoft.Extensions.Logging.Abstractions;
using PavlovBot.Core.Data;
using PavlovBot.Host.Configuration;
using PavlovBot.Host.Discord;
using PavlovBot.Host.Moderation;
using PavlovBot.Host.Discord.Commands;
using PavlovBot.Host.Factions;
using PavlovBot.Host.Observability;
using PavlovBot.Host.Rcon;
using PavlovBot.Host.Storage;
using Xunit;

namespace PavlovBot.Tests;

/// <summary>
/// Who sees a roster change.
/// </summary>
/// <remarks>
/// Ephemeral is a one-line property with no behaviour of its own, which is exactly why it is
/// worth a test: nothing else fails when it flips back, and the symptom is a channel filling
/// with somebody's admin work rather than an error anybody reports.
///
/// THE GATEWAY DEFERS WITH IT. Every reply in the command goes through
/// ModifyOriginalResponseAsync, which inherits the deferral - so this property alone decides
/// the visibility of all of them, including the wipe confirmation.
/// </remarks>
public class WhitelistEphemeralTests
{
    private static SerializedStore Store() => new(new MemoryBackend(), new SystemTextJsonCodec());

    private static RconRegistry Rcon(SerializedStore store) => new(
        new BotOptions
        {
            DiscordToken = "t",
            Servers = [],
            Monitoring = new MonitoringOptions(null, "127.0.0.1", null),
            DataDirectory = Path.GetTempPath(),
        }, new MetricsRegistry(), NullLogger<RconRegistry>.Instance);

    private static WhitelistCommand Whitelist()
    {
        var store = Store();
        return new WhitelistCommand(
            new RosterService(null, NullLogger<RosterService>.Instance),
            new FactionMembers(store),
            new Access(store, [], []),
            new Boards(store, Rcon(store)),
            new AuditLog(store),
            NullLogger<WhitelistCommand>.Instance);
    }

    [Fact]
    public void WhitelistRepliesAreVisibleOnlyToWhoeverRanTheCommand()
    {
        Assert.True(Whitelist().Ephemeral);
    }

    /// <summary>A command that says nothing about visibility, so it gets the default.</summary>
    private sealed class Plain : ISlashCommand
    {
        public string Name => "plain";
        public Discord.ApplicationCommandProperties Build() => throw new NotSupportedException();
        public Task HandleAsync(Discord.WebSocket.SocketSlashCommand c, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    [Fact]
    public void TheInterfaceDefaultIsStillPublic()
    {
        /* The control. If ISlashCommand's default ever became true, the assertion above
           would pass while proving nothing about this command in particular. */
        // Through the interface: the default is a default INTERFACE member, so it does not
        // exist on the concrete type at all - which is also why overriding it is a visible act.
        Assert.False(((ISlashCommand)new Plain()).Ephemeral);
    }
}
