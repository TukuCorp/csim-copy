using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CarbonSim.Engine.Bots;

namespace CarbonSim.Engine.Tests;

/// <summary>
/// A fingerprint of everything the bots did, in the order they did it. The bot log is the net the
/// architecture review asks for before anything moves the seeded draw order (candidate 6: splitting
/// a bot's plan from its execution), because a reordered draw changes the run without changing any
/// rule. The numbers inside a bot's own wording are folded to their digits, so a machine whose
/// culture writes 100,00 measures the same digest as one that writes 100.00.
/// </summary>
internal static partial class BotLog
{
    /// <summary>How many things the bots did, and the SHA-256 of the log in order.</summary>
    public static (int Count, string Digest) Summarise(IReadOnlyList<BotAction> actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        StringBuilder text = new();

        foreach (BotAction action in actions)
        {
            text.Append(action.Year).Append('|')
                .Append(action.Company).Append('|')
                .Append(action.Trigger).Append('|')
                .Append(Fold(action.Detail)).Append('\n');
        }

        return (actions.Count, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString()))).ToLowerInvariant());
    }

    /// <summary>Keeps the digits of a number and drops the separators a culture puts in it.</summary>
    private static string Fold(string detail) =>
        Digits().Replace(detail, match => match.Value.Replace(".", string.Empty, StringComparison.Ordinal).Replace(",", string.Empty, StringComparison.Ordinal));

    [GeneratedRegex("[0-9][0-9.,]*[0-9]|[0-9]")]
    private static partial Regex Digits();
}
