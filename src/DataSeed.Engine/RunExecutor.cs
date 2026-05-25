using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DataSeed.Engine.Models;
using DataSeed.Schema.Models;

namespace DataSeed.Engine;

public class RunExecutor
{
    public string Execute(
        DomainSchema schema,
        PlanFile plan,
        string outputBase,
        bool compact = false,
        PlanFile? catalogPlan = null,
        int? seedOverride = null)
    {
        var folderName = OutputNamer.Generate();
        var outputDir = Path.Combine(outputBase, folderName);
        Directory.CreateDirectory(outputDir);

        var seed = seedOverride ?? plan.Seed;
        var mergedPlan = MergeCatalog(plan, catalogPlan);

        // Generated entity records, keyed by entity name
        var generatedEntities = new Dictionary<string, List<Dictionary<string, object?>>>(
            StringComparer.OrdinalIgnoreCase);

        // Pre-populate from reference and taxonomy entities
        PopulateReferenceAndTaxonomy(schema, mergedPlan, generatedEntities);

        var sorted = DependencyGraph.TopologicalSort(schema.Entities);
        var runner = new BogusRunner(seed, generatedEntities);

        var entityCounts = new Dictionary<string, int>();
        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = !compact,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        foreach (var entity in sorted)
        {
            if (!mergedPlan.Entities.TryGetValue(entity.Name, out var entityPlan))
                continue;

            List<Dictionary<string, object?>> records;

            switch (entityPlan.Type)
            {
                case "reference":
                    records = entityPlan.Resolved;
                    break;

                case "taxonomy":
                    var sep = entityPlan.Separator ?? " > ";
                    var leaves = TaxonomyFlattener.GetLeafPaths(entityPlan.ResolvedTree, sep);
                    records = leaves.Select((path, idx) => new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["id"] = $"{entity.Name[..Math.Min(3, entity.Name.Length)].ToLower()}-{idx + 1:D4}",
                        ["path"] = path,
                        ["name"] = path.Split(sep).Last()
                    }).ToList();
                    break;

                case "dynamic":
                    if (entity.Parent is not null
                        && generatedEntities.TryGetValue(entity.Parent, out var parentRecords))
                    {
                        records = runner.GenerateChildRecords(entity, entityPlan, parentRecords);
                    }
                    else
                    {
                        var count = entityPlan.Count > 0 ? entityPlan.Count : (entity.Count ?? 10);
                        records = runner.GenerateRecords(entity, entityPlan, count);
                    }
                    break;

                default:
                    continue;
            }

            generatedEntities[entity.Name] = records;
            entityCounts[entity.Name] = records.Count;

            var filePath = Path.Combine(outputDir, $"{entity.Name}.json");
            var json = JsonSerializer.Serialize(records, jsonOptions);
            File.WriteAllText(filePath, json);
        }

        WriteManifest(schema, plan, folderName, entityCounts, outputDir, jsonOptions);

        return folderName;
    }

    private static void PopulateReferenceAndTaxonomy(
        DomainSchema schema,
        PlanFile plan,
        Dictionary<string, List<Dictionary<string, object?>>> generatedEntities)
    {
        foreach (var kv in plan.Entities)
        {
            if (kv.Value.Type == "reference")
            {
                generatedEntities[kv.Key] = kv.Value.Resolved;
            }
            else if (kv.Value.Type == "taxonomy")
            {
                var sep = kv.Value.Separator ?? " > ";
                var leaves = TaxonomyFlattener.GetLeafPaths(kv.Value.ResolvedTree, sep);
                generatedEntities[kv.Key] = leaves.Select((path, idx) =>
                    new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["id"] = $"{kv.Key[..Math.Min(3, kv.Key.Length)].ToLower()}-{idx + 1:D4}",
                        ["path"] = path,
                        ["name"] = path.Split(sep).Last()
                    }).ToList();
            }
        }
    }

    private static PlanFile MergeCatalog(PlanFile plan, PlanFile? catalog)
    {
        if (catalog is null) return plan;
        var merged = new PlanFile
        {
            Domain = plan.Domain,
            SchemaFile = plan.SchemaFile,
            GeneratedAt = plan.GeneratedAt,
            Provider = plan.Provider,
            Seed = plan.Seed,
            Entities = new Dictionary<string, EntityPlan>(plan.Entities)
        };
        foreach (var kv in catalog.Entities)
            merged.Entities.TryAdd(kv.Key, kv.Value);
        return merged;
    }

    private static void WriteManifest(
        DomainSchema schema,
        PlanFile plan,
        string folderName,
        Dictionary<string, int> entityCounts,
        string outputDir,
        JsonSerializerOptions options)
    {
        var manifest = new
        {
            domain = schema.Domain,
            generatedAt = DateTime.UtcNow.ToString("O"),
            schemaFile = plan.SchemaFile,
            planFile = Path.ChangeExtension(plan.SchemaFile, ".plan.yaml"),
            provider = plan.Provider,
            outputFolder = folderName,
            entities = entityCounts
        };
        File.WriteAllText(
            Path.Combine(outputDir, "manifest.json"),
            JsonSerializer.Serialize(manifest, options));
    }
}
