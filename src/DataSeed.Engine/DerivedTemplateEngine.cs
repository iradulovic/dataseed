using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DataSeed.Engine;

public class DerivedTemplateEngine
{
    private readonly Dictionary<string, int> _sequences = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<Dictionary<string, object?>>> _entityRecords;
    private readonly Random _rng;

    public DerivedTemplateEngine(
        Dictionary<string, List<Dictionary<string, object?>>> entityRecords,
        int seed)
    {
        _entityRecords = entityRecords;
        _rng = new Random(seed);
    }

    public string Evaluate(string template)
    {
        // Replace {sequence:N} — N is the zero-pad width
        var result = Regex.Replace(template, @"\{sequence:(\d+)\}", m =>
        {
            var width = int.Parse(m.Groups[1].Value);
            var key = $"__seq_{width}";
            if (!_sequences.TryGetValue(key, out var current))
                current = 0;
            _sequences[key] = current + 1;
            return (current + 1).ToString().PadLeft(width, '0');
        });

        // Replace {entityName.propertyName} — picks random record from already-generated entity
        result = Regex.Replace(result, @"\{(\w+)\.(\w+)\}", m =>
        {
            var entityName = m.Groups[1].Value;
            var propName = m.Groups[2].Value;
            if (!_entityRecords.TryGetValue(entityName, out var records) || records.Count == 0)
                return string.Empty;
            var record = records[_rng.Next(records.Count)];
            return record.TryGetValue(propName, out var val) ? val?.ToString() ?? string.Empty : string.Empty;
        });

        return result;
    }

    public void ResetSequences() => _sequences.Clear();
}
