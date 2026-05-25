using System;
using System.Collections.Generic;
using System.Linq;
using Bogus;
using DataSeed.Engine.Models;
using DataSeed.Schema.Models;

namespace DataSeed.Engine;

public class BogusRunner
{
    private readonly int _seed;
    private readonly Random _rng;
    private readonly Dictionary<string, List<Dictionary<string, object?>>> _generatedEntities;
    private readonly DerivedTemplateEngine _templateEngine;

    public BogusRunner(int seed, Dictionary<string, List<Dictionary<string, object?>>> generatedEntities)
    {
        _seed = seed;
        _rng = new Random(seed);
        _generatedEntities = generatedEntities;
        _templateEngine = new DerivedTemplateEngine(generatedEntities, seed);
    }

    public List<Dictionary<string, object?>> GenerateRecords(
        EntityDefinition entity,
        EntityPlan plan,
        int count,
        string? parentId = null)
    {
        var faker = new Faker { Random = new Randomizer(_seed) };
        var qualityBuckets = BuildQualityBuckets(entity.QualityProfile, count);
        var records = new List<Dictionary<string, object?>>(count);

        for (int i = 0; i < count; i++)
        {
            var profile = qualityBuckets[i];
            var record = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            var prefix = entity.Name.Length >= 3
                ? entity.Name[..3].ToLower()
                : entity.Name.ToLower();
            record["id"] = $"{prefix}-{Guid.NewGuid():N}".ToLower();

            if (parentId is not null)
                record["parentId"] = parentId;

            foreach (var prop in entity.Properties)
            {
                if (!plan.PropertyStrategies.TryGetValue(prop.Name, out var strategy))
                {
                    record[prop.Name] = null;
                    continue;
                }

                var value = GenerateValue(faker, prop.Name, entity.Name, strategy, profile);
                record[prop.Name] = value;
            }

            records.Add(record);
        }

        return records;
    }

    public List<Dictionary<string, object?>> GenerateChildRecords(
        EntityDefinition entity,
        EntityPlan plan,
        List<Dictionary<string, object?>> parentRecords)
    {
        var all = new List<Dictionary<string, object?>>();

        // Find linesPerParent strategy
        (int min, int max) lines = (1, 5);
        if (plan.PropertyStrategies.TryGetValue("__linesPerParent", out var lpStrategy)
            && lpStrategy.LinesPerParent.HasValue)
        {
            lines = lpStrategy.LinesPerParent.Value;
        }

        foreach (var parent in parentRecords)
        {
            var parentId = parent.TryGetValue("id", out var id) ? id?.ToString() : null;
            var count = _rng.Next(lines.min, lines.max + 1);
            var children = GenerateRecords(entity, plan, count, parentId);
            all.AddRange(children);
        }

        return all;
    }

    private object? GenerateValue(Faker faker, string propName, string entityName, PropertyStrategy strategy, string? profile)
    {
        // Apply null probability
        if (strategy.NullPercent.HasValue && _rng.Next(100) < strategy.NullPercent.Value)
            return null;

        var rawValue = strategy.Bogus switch
        {
            "derived" => strategy.Template is not null
                ? _templateEngine.Evaluate(strategy.Template)
                : faker.Random.AlphaNumeric(8),

            // Must come BEFORE the generic "pickFrom:" arm — "pickFrom:values" would otherwise
            // match that arm first, call PickFromEntity("values", ...) and always return null.
            "pickFrom:values" => strategy.Values.Count > 0
                ? strategy.Values[_rng.Next(strategy.Values.Count)]
                : (object?)null,

            var b when b != null && b.StartsWith("pickFrom:") => PickFromEntity(
                b["pickFrom:".Length..], strategy),

            var b when b != null && b.StartsWith("Random.Int(") => ParseAndGenerateInt(b),
            var b when b != null && b.StartsWith("Random.Decimal(") => ParseAndGenerateDecimal(b),
            var b when b != null && b.Contains('.') => InvokeFaker(faker, b),
            _ => faker.Lorem.Sentence()
        };

        // Apply degradation
        if (strategy.DegradePercent.HasValue
            && rawValue is string strVal
            && _rng.Next(100) < strategy.DegradePercent.Value)
        {
            rawValue = DegradationEngine.Degrade(strVal, strategy.DegradeStrategy ?? "truncate-to-noun", _rng);
        }

        return rawValue;
    }

    private object? PickFromEntity(string spec, PropertyStrategy strategy)
    {
        var parts = spec.Split('.');
        var entityName = parts[0];
        var fieldName = parts.Length > 1 ? parts[1] : "id";

        if (!_generatedEntities.TryGetValue(entityName, out var records) || records.Count == 0)
            return null;

        var distribution = strategy.Distribution ?? "random";
        Dictionary<string, object?> picked;

        if (distribution == "long-tail" && strategy.Skew.HasValue)
        {
            picked = PickLongTail(records, strategy.Skew.Value);
        }
        else if (distribution == "weighted" && _generatedEntities.TryGetValue($"__weights_{entityName}", out var wRecords))
        {
            picked = records[_rng.Next(records.Count)];
        }
        else
        {
            picked = records[_rng.Next(records.Count)];
        }

        return picked.TryGetValue(fieldName, out var val) ? val : null;
    }

    private Dictionary<string, object?> PickLongTail(List<Dictionary<string, object?>> records, double skew)
    {
        // Power-law: weight of item i (1-indexed) ∝ 1 / i^skew
        var weights = new double[records.Count];
        double sum = 0;
        for (int i = 0; i < records.Count; i++)
        {
            weights[i] = 1.0 / Math.Pow(i + 1, skew);
            sum += weights[i];
        }
        var r = _rng.NextDouble() * sum;
        double cumulative = 0;
        for (int i = 0; i < records.Count; i++)
        {
            cumulative += weights[i];
            if (r <= cumulative) return records[i];
        }
        return records[^1];
    }

    private object? ParseAndGenerateInt(string bogus)
    {
        var m = System.Text.RegularExpressions.Regex.Match(bogus, @"Random\.Int\((\d+),(\d+)\)");
        if (!m.Success) return _rng.Next(1, 100);
        return _rng.Next(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value) + 1);
    }

    private object? ParseAndGenerateDecimal(string bogus)
    {
        var m = System.Text.RegularExpressions.Regex.Match(bogus, @"Random\.Decimal\((\d+(?:\.\d+)?),(\d+(?:\.\d+)?)\)");
        if (!m.Success) return Math.Round(_rng.NextDouble() * 100, 2);
        var min = double.Parse(m.Groups[1].Value);
        var max = double.Parse(m.Groups[2].Value);
        return Math.Round(min + _rng.NextDouble() * (max - min), 2);
    }

    private static object? InvokeFaker(Faker faker, string bogus)
    {
        try
        {
            var dot = bogus.IndexOf('.');
            if (dot < 0) return faker.Lorem.Word();
            var category = bogus[..dot];
            var rest = bogus[(dot + 1)..];
            var parenIdx = rest.IndexOf('(');
            var methodName = parenIdx >= 0 ? rest[..parenIdx] : rest;

            return category.ToLowerInvariant() switch
            {
                "name" => methodName switch
                {
                    "FullName" => faker.Name.FullName(),
                    "FirstName" => faker.Name.FirstName(),
                    "LastName" => faker.Name.LastName(),
                    _ => faker.Name.FullName()
                },
                "company" => faker.Company.CompanyName(),
                "address" => methodName switch
                {
                    "StreetAddress" => faker.Address.StreetAddress(),
                    "SecondaryAddress" => faker.Address.SecondaryAddress(),
                    "City" => faker.Address.City(),
                    "StateAbbr" => faker.Address.StateAbbr(),
                    "State" => faker.Address.State(),
                    "ZipCode" => faker.Address.ZipCode(),
                    "Country" => faker.Address.Country(),
                    _ => faker.Address.FullAddress()
                },
                "internet" => methodName switch
                {
                    "Email" => faker.Internet.Email(),
                    "Url" => faker.Internet.Url(),
                    "UserName" => faker.Internet.UserName(),
                    _ => faker.Internet.Email()
                },
                "phone" => faker.Phone.PhoneNumber(),
                "commerce" => methodName switch
                {
                    "ProductName" => faker.Commerce.ProductName(),
                    "ProductDescription" => faker.Commerce.ProductDescription(),
                    "Department" => faker.Commerce.Department(),
                    "Price" => faker.Commerce.Price(),
                    _ => faker.Commerce.ProductName()
                },
                "lorem" => methodName switch
                {
                    "Word" => faker.Lorem.Word(),
                    "Words" => string.Join(" ", faker.Lorem.Words()),
                    "Sentence" => faker.Lorem.Sentence(),
                    "Paragraph" => faker.Lorem.Paragraph(),
                    _ => faker.Lorem.Sentence()
                },
                "date" => methodName switch
                {
                    "Past" => faker.Date.Past().ToString("yyyy-MM-dd"),
                    "Future" => faker.Date.Future().ToString("yyyy-MM-dd"),
                    "Recent" => faker.Date.Recent().ToString("yyyy-MM-dd"),
                    _ => faker.Date.Past().ToString("yyyy-MM-dd")
                },
                "random" => methodName switch
                {
                    "AlphaNumeric" => faker.Random.AlphaNumeric(8),
                    _ => faker.Random.Word()
                },
                _ => faker.Lorem.Word()
            };
        }
        catch
        {
            return faker.Lorem.Word();
        }
    }

    private static List<string?> BuildQualityBuckets(Dictionary<string, string> profile, int count)
    {
        var buckets = new List<string?>(count);
        if (profile.Count == 0)
        {
            for (int i = 0; i < count; i++) buckets.Add(null);
            return buckets;
        }

        var assigned = 0;
        foreach (var kv in profile)
        {
            var pctStr = kv.Value.TrimEnd('%').Trim();
            if (!double.TryParse(pctStr, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var pct))
                continue;
            var n = (int)Math.Round(count * pct / 100.0);
            for (int i = 0; i < n && assigned < count; i++, assigned++)
                buckets.Add(kv.Key);
        }

        while (buckets.Count < count) buckets.Add(null);

        // Shuffle for variety
        var rng = new Random();
        for (int i = buckets.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (buckets[i], buckets[j]) = (buckets[j], buckets[i]);
        }

        return buckets;
    }
}
