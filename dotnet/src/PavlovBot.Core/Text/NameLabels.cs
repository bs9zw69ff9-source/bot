namespace PavlovBot.Core.Text;

/// <summary>
/// The parenthesised tags the bot puts after a player's name, in one place.
/// </summary>
/// <remarks>
/// THESE COME BACK. A tag is added for display - "Alice (online)" in an autocomplete list,
/// "Bob (manual entry)" when nothing matched what was typed - and the name then returns from
/// Discord, or gets read off the screen and typed into a command that has no autocomplete,
/// with the tag still attached. So whatever adds one has to be undone at every point a name
/// is accepted.
///
/// IT WAS TWO LISTS AND THEY DRIFTED. <see cref="Sanitize.Id"/> stripped "manual entry" and
/// "offline"; the autocomplete had moved on to emitting "online" and "recent". The two it
/// stripped were no longer produced and the two produced were not stripped - so "(online)"
/// survived the strip, met the character filter that removes brackets and spaces, and
/// arrived as a ban on "Aliceonline": a player who does not exist, which looks exactly like
/// a ban that worked.
///
/// One list, used by the code that adds a tag AND the code that removes one, with a test
/// asserting every entry round-trips. Adding a tag anywhere else is the bug coming back, so
/// add it here instead.
/// </remarks>
public static class NameLabels
{
    /// <summary>
    /// Every tag, without its brackets.
    /// </summary>
    /// <remarks>
    /// "offline" is kept although nothing emits it any more. Names carrying it were written
    /// into rosters and ban records by earlier builds, and those files are still read.
    /// </remarks>
    public static IReadOnlyList<string> All { get; } =
    [
        "online",
        "offline",
        "recent",
        "manual entry",
        "banned",
        "nobody online",
    ];

    /// <summary>Append a tag for display. An empty tag leaves the name alone.</summary>
    public static string Decorate(string name, string? tag) =>
        string.IsNullOrWhiteSpace(tag) ? name : $"{name} ({tag})";
}
