using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DataSeed.Schema.Models;

namespace DataSeed.Schema;

public class ValidationError
{
    public string Message { get; }
    public ValidationError(string message) => Message = message;
    public override string ToString() => Message;
}

public class SchemaValidator
{
    public List<ValidationError> Validate(DomainSchema schema, IEnumerable<string>? externalEntityNames = null)
    {
        var errors = new List<ValidationError>();

        if (string.IsNullOrWhiteSpace(schema.Domain))
            errors.Add(new("Schema must have a non-empty 'domain' field."));

        var namesSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entity in schema.Entities)
        {
            if (string.IsNullOrWhiteSpace(entity.Name))
            {
                errors.Add(new("Entity has an empty or missing name."));
                continue;
            }
            if (!namesSeen.Add(entity.Name))
                errors.Add(new($"Duplicate entity name: '{entity.Name}'."));
        }

        // Build a safe dictionary (first-wins on duplicates, already reported above)
        var entityByName = new Dictionary<string, EntityDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in schema.Entities)
            entityByName.TryAdd(e.Name, e);

        // Known entity names = local + external (catalog)
        var allKnownNames = new HashSet<string>(entityByName.Keys, StringComparer.OrdinalIgnoreCase);
        foreach (var ext in externalEntityNames ?? Enumerable.Empty<string>())
            allKnownNames.Add(ext);

        foreach (var entity in schema.Entities)
        {
            if (entity.Type == EntityType.Taxonomy && (entity.Depth ?? 0) < 1)
                errors.Add(new($"Taxonomy entity '{entity.Name}' must have depth >= 1."));

            if (entity.Parent is not null)
            {
                if (!entityByName.TryGetValue(entity.Parent, out var parent))
                    errors.Add(new($"Entity '{entity.Name}' references unknown parent '{entity.Parent}'."));
                else if (parent.Type != EntityType.Dynamic)
                    errors.Add(new($"Entity '{entity.Name}' parent '{entity.Parent}' must be a dynamic entity."));
            }

            if (entity.QualityProfile.Count > 0)
            {
                var total = 0.0;
                foreach (var kv in entity.QualityProfile)
                {
                    var pct = ParsePercent(kv.Value);
                    if (pct is null)
                        errors.Add(new($"Entity '{entity.Name}' quality profile key '{kv.Key}' has invalid percentage '{kv.Value}'."));
                    else
                        total += pct.Value;
                }
                if (total > 100.01)
                    errors.Add(new($"Entity '{entity.Name}' quality profile percentages sum to {total:F1}%, must be <= 100%."));
            }

            foreach (var prop in entity.Properties)
            {
                if (prop.Ref is not null && externalEntityNames is not null)
                {
                    // Only flag unknown refs when caller supplies a definitive list of known entities
                    if (!allKnownNames.Contains(prop.Ref))
                        errors.Add(new($"Property '{entity.Name}.{prop.Name}' references unknown entity '{prop.Ref}'."));
                }

                var hints = HintParser.Parse(prop.Hints);
                if (hints.DerivedTemplate is not null && externalEntityNames is not null)
                {
                    foreach (Match m in Regex.Matches(hints.DerivedTemplate, @"\{(\w+)\.(\w+)\}"))
                    {
                        var refName = m.Groups[1].Value;
                        if (!allKnownNames.Contains(refName))
                            errors.Add(new($"Property '{entity.Name}.{prop.Name}' derived template references unknown entity '{refName}'."));
                    }
                }
            }
        }

        return errors;
    }

    private static double? ParsePercent(string value)
    {
        var s = value.TrimEnd('%').Trim();
        return double.TryParse(s, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : null;
    }
}
