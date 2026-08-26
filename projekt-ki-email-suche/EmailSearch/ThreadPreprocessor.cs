using System.Text;
using System.Text.RegularExpressions;

namespace EmailSearch;

public static partial class ThreadPreprocessor
{
    // Entfernt zitierten Text aus Antwort-Mails (z. B. "Am 12.08.2026 schrieb ...:" oder "-----Ursprüngliche Nachricht-----"),
    // damit derselbe Inhalt nicht mehrfach im Thread auftaucht.
    public static string StripQuotedText(string body)
    {
        var lines = body.Split('\n');
        var result = new StringBuilder();

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');

            if (QuoteHeaderRegex().IsMatch(line) || OriginalMessageRegex().IsMatch(line))
            {
                break; // alles danach ist Zitat der vorherigen Mail
            }

            if (line.TrimStart().StartsWith('>'))
            {
                continue;
            }

            result.AppendLine(line);
        }

        return result.ToString().Trim();
    }

    public static List<string> ExtractDates(string text) =>
        DateRegex().Matches(text).Select(m => m.Value).Distinct().ToList();

    public static List<string> ExtractAmounts(string text) =>
        AmountRegex().Matches(text).Select(m => m.Value).Distinct().ToList();

    [GeneratedRegex(@"^(Am|On)\s.+\s(schrieb|wrote)\s*.*:\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex QuoteHeaderRegex();

    [GeneratedRegex(@"^-{3,}\s*(Ursprüngliche Nachricht|Original Message|Weitergeleitete Nachricht|Forwarded message)\s*-{0,}", RegexOptions.IgnoreCase)]
    private static partial Regex OriginalMessageRegex();

    [GeneratedRegex(@"\b\d{1,2}\.\d{1,2}\.\d{2,4}\b")]
    private static partial Regex DateRegex();

    [GeneratedRegex(@"\b\d{1,3}(?:\.\d{3})*,\d{2}\s?€|\b\d+,\d{2}\s?EUR\b")]
    private static partial Regex AmountRegex();
}
