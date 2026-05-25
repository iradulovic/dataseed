using System;
using System.IO;
using System.Collections.Generic;
using DataSeed.Schema.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DataSeed.Schema;

public class SchemaParser
{
    private readonly IDeserializer _deserializer;

    public SchemaParser()
    {
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    public DomainSchema ParseFile(string path)
    {
        var yaml = File.ReadAllText(path);
        return ParseYaml(yaml);
    }

    public DomainSchema ParseYaml(string yaml)
    {
        var raw = _deserializer.Deserialize<RawSchema>(yaml);
        return Map(raw);
    }

    private static DomainSchema Map(RawSchema raw)
    {
        var schema = new DomainSchema
        {
            Domain = raw.Domain ?? string.Empty,
            Description = raw.Description ?? string.Empty
        };

        foreach (var re in raw.Entities ?? new())
        {
            var entity = new EntityDefinition
            {
                Name = re.Name ?? string.Empty,
                Type = ParseType(re.Type),
                Count = re.Count,
                Description = re.Description,
                Parent = re.Parent,
                Depth = re.Depth,
                Separator = re.Separator,
                MustInclude = re.MustInclude ?? new(),
                Hints = NormalizeHints(re.Hints),
                QualityProfile = ParseQualityProfile(re.QualityProfile)
            };

            foreach (var rp in re.Properties ?? new())
            {
                entity.Properties.Add(new PropertyDefinition
                {
                    Name = rp.Name ?? string.Empty,
                    Description = rp.Description,
                    Ref = rp.Ref,
                    Examples = rp.Examples ?? new(),
                    Hints = NormalizeHints(rp.Hints)
                });
            }

            schema.Entities.Add(entity);
        }

        return schema;
    }

    private static EntityType ParseType(string? raw) => raw?.ToLowerInvariant() switch
    {
        "reference" => EntityType.Reference,
        "taxonomy" => EntityType.Taxonomy,
        "dynamic" => EntityType.Dynamic,
        _ => EntityType.Dynamic
    };

    private static List<string> NormalizeHints(object? raw)
    {
        if (raw is null) return new();
        if (raw is List<object> list)
        {
            var result = new List<string>();
            foreach (var item in list)
            {
                if (item is string s) result.Add(s);
                else if (item is Dictionary<object, object> dict)
                {
                    foreach (var kv in dict)
                        result.Add($"{kv.Key}: {kv.Value}");
                }
            }
            return result;
        }
        return new();
    }

    private static Dictionary<string, string> ParseQualityProfile(Dictionary<string, object>? raw)
    {
        var result = new Dictionary<string, string>();
        if (raw is null) return result;
        foreach (var kv in raw)
            result[kv.Key] = kv.Value?.ToString() ?? string.Empty;
        return result;
    }

    // Raw YAML mapping classes
    private class RawSchema
    {
        public string? Domain { get; set; }
        public string? Description { get; set; }
        public List<RawEntity>? Entities { get; set; }
    }

    private class RawEntity
    {
        public string? Name { get; set; }
        public string? Type { get; set; }
        public int? Count { get; set; }
        public string? Description { get; set; }
        public string? Parent { get; set; }
        public int? Depth { get; set; }
        public string? Separator { get; set; }
        public List<string>? MustInclude { get; set; }
        public object? Hints { get; set; }
        public List<RawProperty>? Properties { get; set; }
        public Dictionary<string, object>? QualityProfile { get; set; }
    }

    private class RawProperty
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Ref { get; set; }
        public List<string>? Examples { get; set; }
        public object? Hints { get; set; }
    }
}
