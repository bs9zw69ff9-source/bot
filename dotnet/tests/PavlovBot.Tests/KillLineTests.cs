using PavlovBot.Host.Discord;
using Xunit;

namespace PavlovBot.Tests;

/// <summary>
/// The kill line.
/// </summary>
/// <remarks>
/// Kills come from Pavlov.log, which carries names and no headshot flag - so the headshot
/// half of this is exercised but never fed in production. It stays because the line builder
/// is pure and the day a source does carry the flag, the rendering is already right.
/// </remarks>
public class KillLineTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 11, 20, 1, 15, TimeSpan.Zero);

    [Fact]
    public void AnOrdinaryKillNamesBothPlayersAndTheWeapon()
    {
        var line = FeedWebhooks.KillLine("Holosight1", "LxPXHam", "10mmpistol", At);

        Assert.Contains("Holosight1 → LxPXHam", line, StringComparison.Ordinal);
        Assert.Contains("(10mmpistol)", line, StringComparison.Ordinal);
        Assert.DoesNotContain("headshot", line, StringComparison.Ordinal);
    }

    [Fact]
    public void AHeadshotSaysSo()
    {
        var line = FeedWebhooks.KillLine("Holosight1", "LxPXHam", "10mmpistol", At, headshot: true);

        Assert.Contains("(10mmpistol, headshot)", line, StringComparison.Ordinal);
    }

    [Fact]
    public void ASuicideIsNotAnArrow()
    {
        /* A self-kill arrives as killer and killed being the same person, which
           rendered as "Name → Name" and reads like a parser fault every single time. */
        var line = FeedWebhooks.KillLine("Holosight1", "Holosight1", "10mmpistol", At, headshot: true);

        Assert.DoesNotContain("→", line, StringComparison.Ordinal);
        Assert.Contains("Holosight1 killed themselves", line, StringComparison.Ordinal);
        Assert.Contains("headshot", line, StringComparison.Ordinal);
    }

    [Fact]
    public void ASelfKillWithNoWeaponIsADeath_NotASuicide()
    {
        // Pavlov files a fall or a drowning this way. "Killed themselves" is the wrong words
        // for walking off a roof.
        var line = FeedWebhooks.KillLine("LxPXHam", "LxPXHam", null, At);

        Assert.Contains("LxPXHam died", line, StringComparison.Ordinal);
        Assert.DoesNotContain("themselves", line, StringComparison.Ordinal);
    }

    [Fact]
    public void AKillWithNoKillerIsTheWorld()
    {
        Assert.Contains("the world → LxPXHam", FeedWebhooks.KillLine(null, "LxPXHam", null, At), StringComparison.Ordinal);
    }
}
