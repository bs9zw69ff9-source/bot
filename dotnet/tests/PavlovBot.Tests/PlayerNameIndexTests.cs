using Microsoft.Extensions.Logging.Abstractions;
using PavlovBot.Core.Data;
using PavlovBot.Host.Configuration;
using PavlovBot.Host.Observability;
using PavlovBot.Host.Rcon;
using PavlovBot.Host.Storage;
using PavlovBot.Rcon;
using Xunit;

namespace PavlovBot.Tests;

/// <summary>
/// Remembering which display name belongs to the id RCON uses.
/// </summary>
/// <remarks>
/// TWO IDENTIFIER SPACES THAT NEVER MEET. Pavlov.log and the game's ban file carry the EOS
/// id - 32 hex characters beginning 0002 - and the evasion registry is keyed on it. Stats.log
/// records the id RCON targets, which on a Shack server is a plain number. A kill read out of
/// Stats.log therefore has nothing to resolve against except the roster.
///
/// And the roster is not enough on its own: it answers for somebody standing on a server at
/// that instant and nothing else. A player who disconnected after the kill, or a roster left
/// stale because RefreshList was failing, both resolved to nothing - and the raw number went
/// into the feed. Both of those happened.
/// </remarks>
public class PlayerNameIndexTests
{
    private const string ShackId = "32996677456614126";
    private const string EosId = "0002a1b3c4d5e6f708192a3b4c5d6e7f";

    private static RconRegistry Registry(SerializedStore store) => new(
        new BotOptions
        {
            DiscordToken = "t",
            Servers = [],
            Monitoring = new MonitoringOptions(null, "127.0.0.1", null),
            DataDirectory = Path.GetTempPath(),
        }, new MetricsRegistry(), NullLogger<RconRegistry>.Instance, store);

    [Fact]
    public void TheTwoIdShapesAreNotTheSameThing()
    {
        /* Pinned because the first version of this feature called the Stats.log value an EOS
           id and fell back to a registry keyed on the other space - a lookup that could never
           hit. The shapes are the tell. */
        Assert.Equal(32, EosId.Length);
        Assert.StartsWith("0002", EosId, StringComparison.Ordinal);
        Assert.True(ShackId.All(char.IsAsciiDigit), "the id RCON targets is a plain number");
        Assert.NotEqual(EosId.Length, ShackId.Length);
    }

    [Fact]
    public void AnIdNeverSeenResolvesToNothing()
    {
        // Not to an empty string, and not to a guess - the caller prints the raw id, which is
        // the only handle anybody has on a player nobody can name.
        Assert.Null(Registry(new SerializedStore(new MemoryBackend(), new SystemTextJsonCodec())).NameForId(ShackId));
        Assert.Null(Registry(new SerializedStore(new MemoryBackend(), new SystemTextJsonCodec())).NameForId(null));
    }

    [Fact]
    public async Task ANameLearnedByOneRegistryIsFoundByTheNext()
    {
        /* THE POINT. The index outlives the roster, the player's session and the process -
           and a restart is exactly when a kill feed is most likely to be printing ids,
           because no roster has been fetched yet. */
        var store = new SerializedStore(new MemoryBackend(), new SystemTextJsonCodec());

        await store.UpdateMapAsync(Datasets.PlayerNames,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            map => { map[ShackId] = "Holosight1"; return map; });

        Assert.Equal("Holosight1", Registry(store).NameForId(ShackId));
    }

    [Fact]
    public async Task TheIndexIsCaseInsensitiveOnTheId()
    {
        // Numbers make this moot, but the field is a string and nothing guarantees the shape
        // stays numeric across a game update - it already changed once.
        var store = new SerializedStore(new MemoryBackend(), new SystemTextJsonCodec());

        await store.UpdateMapAsync(Datasets.PlayerNames,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            map => { map[EosId] = "Alice"; return map; });

        Assert.Equal("Alice", Registry(store).NameForId(EosId.ToUpperInvariant()));
    }
}
