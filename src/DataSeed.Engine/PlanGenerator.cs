using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DataSeed.Engine.Models;
using DataSeed.Schema;
using DataSeed.Schema.Models;

namespace DataSeed.Engine;

public class PlanGenerator
{
    private readonly ILlmProvider _provider;

    public PlanGenerator(ILlmProvider provider) => _provider = provider;

    public async Task<PlanFile> GenerateAsync(
        DomainSchema schema,
        string schemaFile,
        string providerName,
        PlanFile? catalogPlan = null,
        CancellationToken ct = default)
    {
        var plan = new PlanFile
        {
            Domain = schema.Domain,
            SchemaFile = schemaFile,
            GeneratedAt = DateTime.UtcNow,
            Provider = providerName,
            Seed = new Random().Next(1, int.MaxValue)
        };

        // Inject catalog plan entities
        if (catalogPlan is not null)
        {
            foreach (var kv in catalogPlan.Entities)
                plan.Entities[kv.Key] = kv.Value;
        }

        var sorted = DependencyGraph.TopologicalSort(schema.Entities);

        foreach (var entity in sorted)
        {
            // Skip if already resolved from catalog
            if (plan.Entities.ContainsKey(entity.Name))
                continue;

            var entityPlan = entity.Type switch
            {
                EntityType.Reference => await BuildReferenceEntityPlan(schema, entity, ct),
                EntityType.Taxonomy => await BuildTaxonomyEntityPlan(schema, entity, ct),
                EntityType.Dynamic => BuildDynamicEntityPlan(schema, entity),
                _ => throw new InvalidOperationException($"Unknown entity type: {entity.Type}")
            };

            plan.Entities[entity.Name] = entityPlan;
        }

        return plan;
    }

    private async Task<EntityPlan> BuildReferenceEntityPlan(DomainSchema schema, EntityDefinition entity, CancellationToken ct)
    {
        var prompt = PromptBuilder.BuildReferencePrompt(schema, entity);
        var json = await _provider.CompleteAsync(prompt, ct);

        var records = new List<Dictionary<string, object?>>();
        using var doc = JsonDocument.Parse(json);
        var arr = doc.RootElement.ValueKind == JsonValueKind.Array
            ? doc.RootElement
            : doc.RootElement.TryGetProperty("records", out var r) ? r
            : throw new InvalidOperationException("Reference LLM response is not a JSON array.");

        foreach (var el in arr.EnumerateArray())
        {
            var record = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in el.EnumerateObject())
                record[prop.Name] = JsonValueToObject(prop.Value);
            records.Add(record);
        }

        return new EntityPlan { Type = "reference", Resolved = records };
    }

    private async Task<EntityPlan> BuildTaxonomyEntityPlan(DomainSchema schema, EntityDefinition entity, CancellationToken ct)
    {
        var prompt = PromptBuilder.BuildTaxonomyPrompt(schema, entity);
        var json = await _provider.CompleteAsync(prompt, ct);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        List<TaxonomyNode> tree;
        Dictionary<string, double> weights;

        if (root.ValueKind == JsonValueKind.Array)
        {
            tree = ParseTree(root);
            weights = new();
        }
        else
        {
            tree = root.TryGetProperty("tree", out var treeEl)
                ? ParseTree(treeEl)
                : ParseTree(root);

            weights = new();
            if (root.TryGetProperty("weights", out var weightsEl))
            {
                foreach (var kv in weightsEl.EnumerateObject())
                    weights[kv.Name] = kv.Value.GetDouble();
            }
        }

        // If no weights provided, compute uniform weights over leaf paths
        if (weights.Count == 0)
        {
            var sep = entity.Separator ?? " > ";
            var leaves = TaxonomyFlattener.GetLeafPaths(tree, sep);
            if (leaves.Count > 0)
            {
                var w = Math.Round(1.0 / leaves.Count, 4);
                foreach (var leaf in leaves)
                    weights[leaf] = w;
            }
        }

        return new EntityPlan
        {
            Type = "taxonomy",
            Separator = entity.Separator ?? " > ",
            ResolvedTree = tree,
            Weights = weights
        };
    }

    private EntityPlan BuildDynamicEntityPlan(DomainSchema schema, EntityDefinition entity)
    {
        var strategies = new Dictionary<string, PropertyStrategy>();

        foreach (var prop in entity.Properties)
        {
            var hints = HintParser.Parse(prop.Hints);
            var strategy = new PropertyStrategy();

            if (hints.DerivedTemplate is not null)
            {
                strategy.Bogus = "derived";
                strategy.Template = hints.DerivedTemplate;
            }
            else if (prop.Ref is not null)
            {
                strategy.Bogus = $"pickFrom:{prop.Ref}.id";
                strategy.Distribution = hints.Distribution ?? "random";
                strategy.Skew = hints.Skew;
            }
            else if (hints.Values.Count > 0)
            {
                strategy.Bogus = "pickFrom:values";
                strategy.Values = hints.Values;
            }
            else if (hints.Range.HasValue)
            {
                strategy.Bogus = $"Random.Int({hints.Range.Value.Min},{hints.Range.Value.Max})";
                strategy.Range = hints.Range;
            }
            else
            {
                strategy.Bogus = InferBogusMethod(prop);
            }

            strategy.NullPercent = hints.NullablePercent;
            strategy.DegradePercent = hints.DegradablePercent;
            if (hints.DegradablePercent.HasValue)
                strategy.DegradeStrategy = "truncate-to-noun";

            strategies[prop.Name] = strategy;
        }

        var entityHints = HintParser.Parse(entity.Hints);
        if (entityHints.LinesPerParent.HasValue)
        {
            strategies["__linesPerParent"] = new PropertyStrategy
            {
                LinesPerParent = entityHints.LinesPerParent
            };
        }

        return new EntityPlan
        {
            Type = "dynamic",
            Count = entity.Count ?? 0,
            PropertyStrategies = strategies
        };
    }

    private static string InferBogusMethod(DataSeed.Schema.Models.PropertyDefinition prop)
    {
        var name = prop.Name.ToLowerInvariant();
        var desc = (prop.Description ?? string.Empty).ToLowerInvariant();

        if (name.Contains("name") && (desc.Contains("company") || desc.Contains("supplier") || desc.Contains("customer")))
            return "Company.CompanyName()";
        if (name.Contains("name") && desc.Contains("person")) return "Name.FullName()";
        if (name == "name") return "Name.FullName()";
        if (name.Contains("address1") || name.Contains("street")) return "Address.StreetAddress()";
        if (name.Contains("address2")) return "Address.SecondaryAddress()";
        if (name.Contains("city")) return "Address.City()";
        if (name.Contains("state")) return "Address.StateAbbr()";
        if (name.Contains("zip") || name.Contains("postal")) return "Address.ZipCode()";
        if (name.Contains("date") || name.Contains("time")) return "Date.Past()";
        if (name.Contains("phone")) return "Phone.PhoneNumber()";
        if (name.Contains("email")) return "Internet.Email()";
        if (name.Contains("price") || name.Contains("amount") || name.Contains("cost"))
            return "Random.Decimal(5,2500)";
        if (name.Contains("quantity") || name.Contains("qty") || name.Contains("count"))
            return "Random.Int(1,100)";
        if (name.Contains("description") || name.Contains("notes") || name.Contains("comment"))
            return "Commerce.ProductDescription()";
        if (name.Contains("sku") || name.Contains("code") || name.Contains("number"))
            return "Random.AlphaNumeric(8)";
        if (name.Contains("location")) return "Address.City()";

        return "Lorem.Sentence()";
    }

    private static List<TaxonomyNode> ParseTree(JsonElement el)
    {
        var result = new List<TaxonomyNode>();
        if (el.ValueKind != JsonValueKind.Array) return result;
        foreach (var item in el.EnumerateArray())
            result.Add(ParseNode(item));
        return result;
    }

    private static TaxonomyNode ParseNode(JsonElement el)
    {
        var node = new TaxonomyNode();
        if (el.TryGetProperty("node", out var n)) node.Node = n.GetString() ?? string.Empty;
        if (el.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
            foreach (var child in children.EnumerateArray())
                node.Children.Add(ParseNode(child));
        return node;
    }

    private static object? JsonValueToObject(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String => el.GetString(),
        JsonValueKind.Number => el.TryGetInt64(out var i) ? i : el.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => el.ToString()
    };
}
