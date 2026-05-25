using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DataSeed.Engine;

public class TemplateResolver : ITemplateResolver
{
    private static readonly Regex TokenPattern = new(@"\{(\w+)\}", RegexOptions.Compiled);

    public string Resolve(
        string template,
        IReadOnlyDictionary<string, IReadOnlyList<string>> parts,
        Random rng)
    {
        return TokenPattern.Replace(template, m =>
        {
            var token = m.Groups[1].Value;
            if (parts.TryGetValue(token, out var values) && values.Count > 0)
                return values[rng.Next(values.Count)];
            return m.Value;
        });
    }
}
