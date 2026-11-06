using System.Globalization;
using System.Text;

namespace KickbaseAssistant.Domain.Players;

public static class TeamNameMatcher
{
    private const int MinimumPrefixLength = 4;

    private static readonly HashSet<string> IgnoredTokens =
    [
        "fc", "sv", "sc", "vfb", "vfl", "tsg", "fsv", "ssv", "rb", "bv", "spvgg", "1", "04", "05", "07", "09",
        "1899", "1846", "1848", "1860", "1900", "1904", "1909", "e", "v",
    ];

    private static readonly Dictionary<string, string> Aliases = new()
    {
        ["munich"] = "munchen",
        ["cologne"] = "koln",
        ["koeln"] = "koln",
        ["muenchen"] = "munchen",
        ["gladbach"] = "monchengladbach",
        ["moenchengladbach"] = "monchengladbach",
        ["hamburger"] = "hamburg",
        ["nuernberg"] = "nurnberg",
        ["nuremberg"] = "nurnberg",
        ["duesseldorf"] = "dusseldorf",
        ["fuerth"] = "furth",
    };

    public static double Similarity(string first, string second)
    {
        var firstTokens = Tokenize(first);
        var secondTokens = Tokenize(second);
        if (firstTokens.Count == 0 || secondTokens.Count == 0)
        {
            return 0;
        }

        var (smaller, larger) = firstTokens.Count <= secondTokens.Count
            ? (firstTokens, secondTokens)
            : (secondTokens, firstTokens);
        var matched = smaller.Count(token => larger.Any(candidate => TokensMatch(token, candidate)));
        return matched / (double)smaller.Count;
    }

    private static bool TokensMatch(string first, string second)
    {
        if (first == second)
        {
            return true;
        }

        var (shorter, longer) = first.Length <= second.Length ? (first, second) : (second, first);
        return shorter.Length >= MinimumPrefixLength && (longer.StartsWith(shorter) || longer.EndsWith(shorter));
    }

    private static List<string> Tokenize(string name)
    {
        var normalized = RemoveDiacritics(name.ToLowerInvariant().Replace("ß", "ss"));
        var tokens = new List<string>();
        var current = new StringBuilder();

        foreach (var character in normalized + " ")
        {
            if (char.IsLetterOrDigit(character))
            {
                current.Append(character);
                continue;
            }

            if (current.Length > 0)
            {
                var token = current.ToString();
                token = Aliases.GetValueOrDefault(token, token);
                if (token.Length > 1 && !IgnoredTokens.Contains(token))
                {
                    tokens.Add(token);
                }

                current.Clear();
            }
        }

        return tokens;
    }

    private static string RemoveDiacritics(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
