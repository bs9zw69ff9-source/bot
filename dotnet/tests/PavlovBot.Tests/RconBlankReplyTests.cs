using PavlovBot.Rcon;
using Xunit;

namespace PavlovBot.Tests;

/// <summary>
/// A blank line arriving where a reply should be.
/// </summary>
/// <remarks>
/// ASSERTED AGAINST THE WIRE. The read loop settled a non-JSON answer on its trailing CRLF,
/// and a bare "\r\n" satisfied that - so a stray blank line was returned AS the answer to
/// whatever had just been sent, while the real reply stayed in the socket for the next
/// command to collect. Every command after that read one reply behind, permanently, because
/// the exchange looked clean and the socket was therefore kept.
///
/// From outside it was three servers all answering RefreshList with nothing, indefinitely.
/// </remarks>
public class RconBlankReplyTests
{
    private static RconOptions Options(FakeRconServer server, int timeoutSeconds = 5) => new()
    {
        Host = "127.0.0.1",
        Port = server.Port,
        Password = server.Password,
        Name = "server1",
        CommandTimeout = TimeSpan.FromSeconds(timeoutSeconds),
        CommandSpacing = TimeSpan.Zero,
        ReadCacheDuration = TimeSpan.Zero,
    };

    private const string Roster =
        "{\"Command\":\"RefreshList\",\"Successful\":true,\"PlayerList\":[{\"Username\":\"Alice\"}]}\r\n";

    [Fact]
    public async Task ABlankLineBeforeTheReplyIsNotMistakenForIt()
    {
        /* THE BUG, on the wire. The blank line arrives first and ends in CRLF and is not
           JSON - every condition the old early return checked. Returning it handed the
           caller nothing and left the roster unread. */
        // SPLIT AFTER THE BLANK LINE, so it arrives in a read of its own. Delivered in one
        // chunk the accumulated text already trims to JSON and the bug never shows - which is
        // why this went unnoticed until a real server chose the break.
        await using var server = new FakeRconServer { ExactReply = "\r\n" + Roster, SplitReplyAfterBytes = 2 };
        await using var client = new RconClient(Options(server));

        var reply = await client.SendAsync("RefreshList");

        Assert.Contains("Alice", reply, StringComparison.Ordinal);
        Assert.Contains("PlayerList", reply, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AReplyOfNothingButWhitespaceIsNeverReturnedAsAReply()
    {
        /* The server says nothing usable at all. The right end to that is the command
           timeout, which leaves the exchange UNSETTLED so the socket is dropped and the next
           command reconnects - a reset is the correct outcome for an exchange that has lost
           track of the stream. Returning "" instead kept the session and made it permanent. */
        await using var server = new FakeRconServer { ExactReply = "\r\n" };
        await using var client = new RconClient(Options(server, timeoutSeconds: 2));

        var thrown = await Record.ExceptionAsync(() => client.SendAsync("RefreshList"));

        Assert.NotNull(thrown);
    }

    [Fact]
    public async Task APlainTextReplyWithActualContentStillSettles()
    {
        /* THE CONTROL. Not every reply is JSON - a refusal or a status line is a real answer
           and has to keep working. Only whitespace is not an answer. */
        await using var server = new FakeRconServer { ExactReply = "Password accepted\r\n" };
        await using var client = new RconClient(Options(server));

        var reply = await client.SendAsync("RefreshList");

        Assert.Contains("Password accepted", reply, StringComparison.Ordinal);
    }
}
