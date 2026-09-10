using Discord;
using PavlovBot.Core.Factions;

namespace PavlovBot.Host.Discord.Commands;

/// <summary>
/// The membership outcomes that are about the BOT'S PLUMBING rather than about the member.
/// </summary>
/// <remarks>
/// Every whitelist command used to end its switch with <c>Theme.Failure("Refused",
/// decision.Outcome.ToString())</c>, which put a raw enum name in front of a moderator -
/// "UnknownFaction" for a faction they had just picked from a dropdown. The name was not even
/// accurate: the same value covered an unset FACTION_ROLES_PATH, an unreadable file and a
/// failed write, three problems with three different fixes.
///
/// Shared rather than repeated in each command because the fix is the same wherever it
/// surfaces, and three copies of it would drift apart the first time one was edited.
/// </remarks>
internal static class MembershipReply
{
    /// <summary>The reply for an outcome a command does not describe itself.</summary>
    internal static EmbedBuilder Fallback(MembershipDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);

        return decision.Outcome switch
        {
            MembershipOutcome.RosterUnavailable => Theme.Failure(
                "The roster files are unreachable",
                "Nothing was changed — the bot cannot reach the roster directory.\n\n" +
                "Check `FACTION_ROLES_PATH` on the process running this command. It has to " +
                "point at the folder holding the faction `.txt` files, and that folder has to " +
                "exist already."),

            MembershipOutcome.WriteFailed => Theme.Failure(
                "The roster could not be written",
                "Nothing was changed. The files read fine but the write failed — permissions, " +
                "a full disk, or the guard blocking a change that would have emptied the " +
                "roster. The log has the reason."),

            MembershipOutcome.UnknownFaction => Theme.Failure(
                "No such faction",
                "That faction is not in the registry. Anyone recorded against it needs " +
                "re-adding to a current one."),

            /* Last resort, and it still names the code. An outcome reaching here is one a
               command forgot to describe, and the enum name is what makes that findable. */
            _ => Theme.Failure("Refused", $"The change was refused: `{decision.Outcome}`."),
        };
    }
}
