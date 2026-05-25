using System;
using System.Linq;

namespace DataSeed.Engine;

public static class DegradationEngine
{
    private static readonly string[] VagueSubstitutes =
    [
        "Plumbing fitting, various sizes",
        "Industrial supply component",
        "Standard pipe assembly part",
        "HVAC equipment component",
        "General plumbing hardware",
        "Miscellaneous industrial fitting",
        "Trade supply item",
        "Commercial grade component",
        "Standard specification part",
        "Distribution supply product"
    ];

    public static string Degrade(string value, string strategy, Random rng)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;

        return strategy switch
        {
            "truncate-to-noun" => TruncateToNoun(value, rng),
            "vague-substitute" => VagueSubstitutes[rng.Next(VagueSubstitutes.Length)],
            _ => TruncateToNoun(value, rng)
        };
    }

    private static string TruncateToNoun(string value, Random rng)
    {
        var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length <= 1) return words.FirstOrDefault() ?? value;
        var take = rng.Next(1, Math.Min(4, words.Length + 1));
        return string.Join(" ", words.Take(take));
    }
}
