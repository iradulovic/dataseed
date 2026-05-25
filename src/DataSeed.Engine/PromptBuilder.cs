using System.Collections.Generic;
using System.Text;
using System.Linq;
using DataSeed.Schema.Models;

namespace DataSeed.Engine;

public static class PromptBuilder
{
    public static string BuildReferencePrompt(DomainSchema schema, EntityDefinition entity)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Respond with valid JSON only. No prose, no markdown fences.");
        sb.AppendLine();
        sb.AppendLine($"Domain context: {schema.Description}");
        sb.AppendLine();
        sb.AppendLine($"Generate exactly {entity.Count} records for entity '{entity.Name}'.");
        if (entity.Description is not null) sb.AppendLine($"Entity description: {entity.Description}");
        sb.AppendLine();
        sb.AppendLine("Properties:");
        foreach (var prop in entity.Properties)
        {
            sb.Append($"  - {prop.Name}");
            if (prop.Description is not null) sb.Append($": {prop.Description}");
            if (prop.Examples.Count > 0) sb.Append($" (examples: {string.Join(", ", prop.Examples)})");
            var hints = DataSeed.Schema.HintParser.Parse(prop.Hints);
            if (hints.Unique) sb.Append(" [UNIQUE across all records]");
            if (hints.Values.Count > 0) sb.Append($" [must be one of: {string.Join(", ", hints.Values)}]");
            sb.AppendLine();
        }
        sb.AppendLine();
        sb.AppendLine("Return a JSON array of objects. Every object must include an 'id' field with a unique identifier.");
        sb.AppendLine("Example format: [{\"id\": \"sup-001\", \"name\": \"...\", ...}, ...]");
        return sb.ToString();
    }

    public static string BuildTaxonomyPrompt(DomainSchema schema, EntityDefinition entity)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Respond with valid JSON only. No prose, no markdown fences.");
        sb.AppendLine();
        sb.AppendLine($"Domain context: {schema.Description}");
        sb.AppendLine();
        sb.AppendLine($"Build a taxonomy tree for '{entity.Name}'.");
        if (entity.Description is not null) sb.AppendLine($"Description: {entity.Description}");
        sb.AppendLine($"Maximum depth: {entity.Depth ?? 3}");
        sb.AppendLine($"Separator used when displaying paths: \"{entity.Separator ?? " > "}\"");
        sb.AppendLine();

        if (entity.MustInclude.Count > 0)
        {
            sb.AppendLine("The following paths MUST appear in the tree (anchor these exactly):");
            foreach (var path in entity.MustInclude)
                sb.AppendLine($"  - {path}");
            sb.AppendLine("Build a coherent tree around these anchors and add additional nodes to fill it out.");
            sb.AppendLine();
        }

        sb.AppendLine("Return a JSON object with two keys:");
        sb.AppendLine("  'tree': array of nodes, each node has 'node' (string) and 'children' (array, same structure)");
        sb.AppendLine("  'weights': object mapping each LEAF PATH (full separator-joined path) to a decimal weight (0.0–1.0, all weights sum to 1.0)");
        sb.AppendLine();
        sb.AppendLine("Example:");
        sb.AppendLine("{");
        sb.AppendLine("  \"tree\": [{\"node\": \"Category A\", \"children\": [{\"node\": \"Sub 1\", \"children\": []}]}],");
        sb.AppendLine("  \"weights\": {\"Category A > Sub 1\": 1.0}");
        sb.AppendLine("}");
        return sb.ToString();
    }

    public static string BuildStructuredTemplatePrompt(
        DomainSchema schema,
        EntityDefinition entity,
        PropertyDefinition prop)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Respond with valid JSON only. No prose, no markdown fences.");
        sb.AppendLine();
        sb.AppendLine($"Domain: {schema.Description}");
        sb.AppendLine($"Entity: {entity.Name} — {entity.Description}");
        sb.AppendLine($"Property: {prop.Name} — {prop.Description}");
        sb.AppendLine();

        var hasQualityProfile = entity.QualityProfile.Count > 0;
        sb.AppendLine($"Produce a JSON structure for generating realistic {prop.Name} values for this entity in this domain.");
        sb.AppendLine("The structure must contain:");
        sb.AppendLine("  \"templates\": object with variant keys. Always include \"default\".");
        if (hasQualityProfile)
        {
            var bucketNames = string.Join(", ", entity.QualityProfile.Keys.Select(k => $"\"{k}\""));
            sb.AppendLine($"  Also include variants for each quality profile bucket: {bucketNames}.");
        }
        sb.AppendLine("  \"parts\": object where each key matches a {token} in the templates, with a \"values\" array of 6-12 realistic domain-specific strings.");
        sb.AppendLine();
        sb.AppendLine("Example:");
        sb.AppendLine("{");
        sb.AppendLine("  \"templates\": {\"default\": \"{prefix}-{series}\"},");
        sb.AppendLine("  \"parts\": {\"prefix\": {\"values\": [\"A\", \"B\"]}, \"series\": {\"values\": [\"100\", \"200\"]}}");
        sb.AppendLine("}");

        return sb.ToString();
    }

    public static string BuildStructuredTemplateWithRefPrompt(
        DomainSchema schema,
        EntityDefinition entity,
        PropertyDefinition prop,
        string refEntityName,
        IEnumerable<string> refValues)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Respond with valid JSON only. No prose, no markdown fences.");
        sb.AppendLine();
        sb.AppendLine($"Domain: {schema.Description}");
        sb.AppendLine($"Entity: {entity.Name} — {entity.Description}");
        sb.AppendLine($"Property: {prop.Name} — {prop.Description}");
        sb.AppendLine($"Referenced entity: {refEntityName}");
        sb.AppendLine();
        sb.AppendLine($"For each of the following {refEntityName} values, produce a JSON structure for generating realistic {prop.Name} values specific to that type.");
        sb.AppendLine();
        sb.AppendLine($"{refEntityName} values:");
        foreach (var v in refValues)
            sb.AppendLine($"  - {v}");
        sb.AppendLine();

        var hasQualityProfile = entity.QualityProfile.Count > 0;
        sb.AppendLine("The response must be a JSON object keyed by the value above. Each value contains:");
        sb.AppendLine("  \"templates\": object with variant keys. Always include \"default\".");
        if (hasQualityProfile)
        {
            var bucketNames = string.Join(", ", entity.QualityProfile.Keys.Select(k => $"\"{k}\""));
            sb.AppendLine($"  Also include variants for each quality profile bucket: {bucketNames}.");
        }
        sb.AppendLine("  \"parts\": object where each key matches a {token} in the templates, with a \"values\" array of 6-12 realistic domain-specific strings for that type.");
        sb.AppendLine();
        sb.AppendLine("Example (for a taxonomy-referenced property):");
        sb.AppendLine("{");
        sb.AppendLine("  \"Category A > Sub 1\": {");
        sb.AppendLine("    \"templates\": {\"default\": \"{size} {material} widget\"},");
        sb.AppendLine("    \"parts\": {\"size\": {\"values\": [\"small\", \"large\"]}, \"material\": {\"values\": [\"steel\", \"brass\"]}}");
        sb.AppendLine("  }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    public static string BuildDynamicStrategyPrompt(DomainSchema schema, EntityDefinition entity)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Respond with valid JSON only. No prose, no markdown fences.");
        sb.AppendLine();
        sb.AppendLine($"Domain context: {schema.Description}");
        sb.AppendLine();
        sb.AppendLine($"For dynamic entity '{entity.Name}', provide a Bogus/Faker generation strategy for each property.");
        if (entity.Description is not null) sb.AppendLine($"Entity description: {entity.Description}");
        sb.AppendLine();
        sb.AppendLine("Properties to generate strategies for:");
        foreach (var prop in entity.Properties)
        {
            sb.Append($"  - {prop.Name}");
            if (prop.Description is not null) sb.Append($": {prop.Description}");
            var hints = DataSeed.Schema.HintParser.Parse(prop.Hints);
            if (hints.Values.Count > 0) sb.Append($" [values: {string.Join(", ", hints.Values)}]");
            if (hints.Range.HasValue) sb.Append($" [range: {hints.Range.Value.Min}-{hints.Range.Value.Max}]");
            if (prop.Ref is not null) sb.Append($" [foreign key to {prop.Ref}]");
            if (hints.DerivedTemplate is not null) sb.Append($" [derived: {hints.DerivedTemplate}]");
            sb.AppendLine();
        }
        sb.AppendLine();
        sb.AppendLine("Return a JSON object keyed by property name. Each value is a strategy object with:");
        sb.AppendLine("  'bogus': Bogus method to call (e.g. 'Name.FullName()', 'Commerce.ProductName()', 'Address.City()', 'Random.Int(1,100)')");
        sb.AppendLine("  'description': brief explanation of what this generates");
        sb.AppendLine();
        sb.AppendLine("Example: {\"name\": {\"bogus\": \"Name.FullName()\", \"description\": \"Full person name\"}}");
        return sb.ToString();
    }
}
