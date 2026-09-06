using PavlovBot.Host.Moderation;
using Xunit;

namespace PavlovBot.Tests;

/// <summary>
/// Reading and writing the file the SERVER enforces.
/// </summary>
/// <remarks>
/// blacklist.txt is one entry per line, the same shape as whitelist.txt and mods.txt beside
/// it. The parser was written for the ModSave mod's ban list instead, which is a three-line
/// block per player separated by blank lines - and a plain list has no blank lines in it, so
/// the WHOLE FILE arrives as one block. Read as a single record it yields the first name and
/// discards every other line as unrecognised metadata: everybody but the first player quietly
/// stops being banned, and the export then writes that truncated list back.
/// </remarks>
public class BanFileFormatTests
{
    [Fact]
    public void APlainListImportsEveryPlayerNotJustTheFirst()
    {
        // THE REGRESSION, and it is silent: the old parser returned exactly one of these.
        var entries = ServerBanFile.Parse("Alice\nBob\nCharlie\n");

        Assert.Equal(["Alice", "Bob", "Charlie"], entries.Select(e => e.Name));
        Assert.All(entries, e => Assert.Equal("Permanent", e.Unban));
    }

    [Fact]
    public void TheOldBlockFormatStillReads()
    {
        /* An install that has been running the previous default has a file in this shape,
           and it has to import cleanly once so those bans reach the store before the export
           rewrites the file. */
        var entries = ServerBanFile.Parse(
            "Alice\nReason: griefing\nUnban: Permanent\n\nBob\nReason: cheating\nUnban: 3d\n");

        Assert.Equal(["Alice", "Bob"], entries.Select(e => e.Name));
        Assert.Equal("griefing", entries[0].Reason);
        Assert.Equal("3d", entries[1].Unban);
    }

    [Fact]
    public void APlainListSeparatedByBlankLinesIsStillOnePlayerPerLine()
    {
        // Hand-edited files grow stray blank lines. Each block is judged on its own.
        var entries = ServerBanFile.Parse("Alice\nBob\n\n\nCharlie\n");

        Assert.Equal(["Alice", "Bob", "Charlie"], entries.Select(e => e.Name));
    }

    [Fact]
    public void CommentsAndBlanksAreNotPlayers()
    {
        var entries = ServerBanFile.Parse("# banned for the tournament\nAlice\n\n// note\nBob\n");

        Assert.Equal(["Alice", "Bob"], entries.Select(e => e.Name));
    }

    [Fact]
    public void AStrayMetadataLineIsNeverReadAsAPlayer()
    {
        /* A trailing blank line in the old format leaves "Reason:" heading its own block.
           Importing that as a player called "Reason" would ban somebody who does not exist
           and then write them back into the file forever. */
        var entries = ServerBanFile.Parse("Alice\nReason: griefing\n\nUnban: Permanent\n");

        Assert.Equal(["Alice"], entries.Select(e => e.Name));
    }

    [Fact]
    public void CarriageReturnsDoNotBecomePartOfTheName()
    {
        // The file is edited on Windows as often as not.
        var entries = ServerBanFile.Parse("Alice\r\nBob\r\n");

        Assert.Equal(["Alice", "Bob"], entries.Select(e => e.Name));
    }

    [Fact]
    public void AnEmptyOrWhitespaceFileBansNobody()
    {
        Assert.Empty(ServerBanFile.Parse(""));
        Assert.Empty(ServerBanFile.Parse("\n\n  \n"));
    }
}
