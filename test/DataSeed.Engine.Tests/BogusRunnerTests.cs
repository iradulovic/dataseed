using System.Collections.Generic;
using System.Linq;
using DataSeed.Engine;
using DataSeed.Engine.Models;
using DataSeed.Schema.Models;
using Xunit;

namespace DataSeed.Engine.Tests;

public class BogusRunnerTests
{
    private static BogusRunner MakeRunner(
        Dictionary<string, List<Dictionary<string, object?>>>? entities = null)
        => new(seed: 42, generatedEntities: entities ?? new());

    private static EntityDefinition SimpleEntity(string name, params PropertyDefinition[] props)
        => new() { Name = name, Type = EntityType.Dynamic, Count = 10, Properties = props.ToList() };

    private static EntityPlan PlanWith(params (string name, PropertyStrategy strategy)[] strategies)
    {
        var plan = new EntityPlan { Type = "dynamic", Count = 10 };
        foreach (var (name, strategy) in strategies)
            plan.PropertyStrategies[name] = strategy;
        return plan;
    }

    // ── pickFrom:values ──────────────────────────────────────────────────────

    [Fact]
    public void PickFromValues_returns_value_from_list()
    {
        var runner = MakeRunner();
        var entity = SimpleEntity("Product", new PropertyDefinition { Name = "uom" });
        var plan = PlanWith(("uom", new PropertyStrategy
        {
            Bogus = "pickFrom:values",
            Values = ["EA", "FT", "LF", "PK"]
        }));

        var records = runner.GenerateRecords(entity, plan, count: 20);

        var allowed = new[] { "EA", "FT", "LF", "PK" };
        Assert.All(records, r =>
        {
            var val = r["uom"]?.ToString();
            Assert.NotNull(val);
            Assert.Contains(val, (IEnumerable<string>)allowed);
        });
    }

    [Fact]
    public void PickFromValues_never_returns_null_when_list_is_populated()
    {
        var runner = MakeRunner();
        var entity = SimpleEntity("Widget", new PropertyDefinition { Name = "status" });
        var plan = PlanWith(("status", new PropertyStrategy
        {
            Bogus = "pickFrom:values",
            Values = ["active", "inactive", "pending"]
        }));

        var records = runner.GenerateRecords(entity, plan, count: 50);

        Assert.All(records, r => Assert.NotNull(r["status"]));
    }

    [Fact]
    public void PickFromValues_returns_null_when_values_list_is_empty()
    {
        var runner = MakeRunner();
        var entity = SimpleEntity("Widget", new PropertyDefinition { Name = "flag" });
        var plan = PlanWith(("flag", new PropertyStrategy
        {
            Bogus = "pickFrom:values",
            Values = []   // empty — should produce null
        }));

        var records = runner.GenerateRecords(entity, plan, count: 5);
        Assert.All(records, r => Assert.Null(r["flag"]));
    }

    [Fact]
    public void PickFromValues_is_not_intercepted_by_generic_pickFrom_arm()
    {
        // Regression: "pickFrom:values" must NOT be routed to PickFromEntity("values", ...)
        // which would look for a generated entity named "values" and always return null.
        var runner = MakeRunner(); // no generated entities registered
        var entity = SimpleEntity("Item", new PropertyDefinition { Name = "type" });
        var plan = PlanWith(("type", new PropertyStrategy
        {
            Bogus = "pickFrom:values",
            Values = ["X", "Y", "Z"]
        }));

        var records = runner.GenerateRecords(entity, plan, count: 10);

        var allowed = new[] { "X", "Y", "Z" };
        // Every record must have a non-null type drawn from the values list
        Assert.All(records, r =>
        {
            Assert.NotNull(r["type"]);
            Assert.Contains(r["type"]!.ToString()!, (IEnumerable<string>)allowed);
        });
    }

    // ── structuredTemplate ───────────────────────────────────────────────────

    [Fact]
    public void StructuredTemplate_no_ref_produces_assembled_string()
    {
        var runner = MakeRunner();
        var entity = SimpleEntity("Customer", new PropertyDefinition { Name = "companyName" });
        var structure = new PropertyStructure
        {
            Templates = new() { ["default"] = "{city} {trade} {type}" },
            Parts = new()
            {
                ["city"] = new StructureParts { Values = ["Midwest", "National"] },
                ["trade"] = new StructureParts { Values = ["Plumbing", "HVAC"] },
                ["type"] = new StructureParts { Values = ["Supply Co.", "Inc."] }
            }
        };
        var plan = new EntityPlan { Type = "dynamic", Count = 10 };
        plan.PropertyStrategies["companyName"] = new PropertyStrategy { Bogus = "structuredTemplate" };
        plan.PropertyStructures["companyName"] = structure;

        var records = runner.GenerateRecords(entity, plan, count: 10);

        Assert.All(records, r =>
        {
            var val = r["companyName"]?.ToString();
            Assert.NotNull(val);
            Assert.NotEmpty(val);
        });
    }

    [Fact]
    public void StructuredTemplate_uses_quality_profile_variant()
    {
        var runner = MakeRunner();
        var entity = new EntityDefinition
        {
            Name = "Product",
            Type = EntityType.Dynamic,
            Count = 10,
            Properties = [new PropertyDefinition { Name = "description" }],
            QualityProfile = new() { ["gold"] = "100%" }
        };
        var structure = new PropertyStructure
        {
            Templates = new()
            {
                ["default"] = "generic item",
                ["gold"] = "{size} premium {material} valve"
            },
            Parts = new()
            {
                ["size"] = new StructureParts { Values = ["1/2 in."] },
                ["material"] = new StructureParts { Values = ["brass"] }
            }
        };
        var plan = new EntityPlan { Type = "dynamic", Count = 10 };
        plan.PropertyStrategies["description"] = new PropertyStrategy { Bogus = "structuredTemplate" };
        plan.PropertyStructures["description"] = structure;

        var records = runner.GenerateRecords(entity, plan, count: 10);

        Assert.All(records, r => Assert.Equal("1/2 in. premium brass valve", r["description"]?.ToString()));
    }

    [Fact]
    public void StructuredTemplate_with_ref_picks_structure_by_ref_value()
    {
        var runner = MakeRunner();
        var entity = SimpleEntity("Product",
            new PropertyDefinition { Name = "categoryPath" },
            new PropertyDefinition { Name = "description" });
        var structure = new PropertyStructure
        {
            Ref = "categoryPath",
            Structures = new()
            {
                ["Valves"] = new RefStructure
                {
                    Templates = new() { ["default"] = "{size} valve" },
                    Parts = new() { ["size"] = new StructureParts { Values = ["1/2 in."] } }
                }
            }
        };
        var plan = new EntityPlan { Type = "dynamic", Count = 2 };
        plan.PropertyStrategies["categoryPath"] = new PropertyStrategy
        {
            Bogus = "pickFrom:values",
            Values = ["Valves"]
        };
        plan.PropertyStrategies["description"] = new PropertyStrategy { Bogus = "structuredTemplate" };
        plan.PropertyStructures["description"] = structure;

        var records = runner.GenerateRecords(entity, plan, count: 5);

        Assert.All(records, r => Assert.Equal("1/2 in. valve", r["description"]?.ToString()));
    }

    [Fact]
    public void StructuredTemplate_ref_unknown_value_uses_fallback()
    {
        var runner = MakeRunner();
        var entity = SimpleEntity("Product",
            new PropertyDefinition { Name = "categoryPath" },
            new PropertyDefinition { Name = "description" });
        var structure = new PropertyStructure
        {
            Ref = "categoryPath",
            Structures = new()
            {
                ["Valves"] = new RefStructure
                {
                    Templates = new() { ["default"] = "{size} valve" },
                    Parts = new() { ["size"] = new StructureParts { Values = ["1/2 in."] } }
                }
            }
        };
        var plan = new EntityPlan { Type = "dynamic", Count = 2 };
        plan.PropertyStrategies["categoryPath"] = new PropertyStrategy
        {
            Bogus = "pickFrom:values",
            Values = ["Unknown Category"]
        };
        plan.PropertyStrategies["description"] = new PropertyStrategy { Bogus = "structuredTemplate" };
        plan.PropertyStructures["description"] = structure;

        var records = runner.GenerateRecords(entity, plan, count: 3);

        // Fallback produces entityName item
        Assert.All(records, r => Assert.Equal("Product item", r["description"]?.ToString()));
    }

    [Fact]
    public void StructuredTemplate_ref_walks_up_path_for_ancestor_match()
    {
        var runner = MakeRunner();
        var entity = SimpleEntity("Product",
            new PropertyDefinition { Name = "categoryPath" },
            new PropertyDefinition { Name = "description" });
        var structure = new PropertyStructure
        {
            Ref = "categoryPath",
            Structures = new()
            {
                ["Plumbing > Valves"] = new RefStructure
                {
                    Templates = new() { ["default"] = "valve item" },
                    Parts = new()
                }
            }
        };
        var plan = new EntityPlan { Type = "dynamic", Count = 1 };
        plan.PropertyStrategies["categoryPath"] = new PropertyStrategy
        {
            Bogus = "pickFrom:values",
            Values = ["Plumbing > Valves > Ball Valves"]
        };
        plan.PropertyStrategies["description"] = new PropertyStrategy { Bogus = "structuredTemplate" };
        plan.PropertyStructures["description"] = structure;

        var records = runner.GenerateRecords(entity, plan, count: 3);

        Assert.All(records, r => Assert.Equal("valve item", r["description"]?.ToString()));
    }

    // ── ref / pickFrom:<Entity>.id ────────────────────────────────────────────

    [Fact]
    public void PickFromEntity_resolves_foreign_key()
    {
        var suppliers = new List<Dictionary<string, object?>>
        {
            new() { ["id"] = "sup-001", ["name"] = "Watts" },
            new() { ["id"] = "sup-002", ["name"] = "Mueller" }
        };
        var runner = MakeRunner(new() { ["Supplier"] = suppliers });

        var entity = SimpleEntity("Product", new PropertyDefinition { Name = "supplierId" });
        var plan = PlanWith(("supplierId", new PropertyStrategy
        {
            Bogus = "pickFrom:Supplier.id",
            Distribution = "random"
        }));

        var records = runner.GenerateRecords(entity, plan, count: 20);
        var knownIds = new[] { "sup-001", "sup-002" };
        Assert.All(records, r => Assert.Contains(r["supplierId"]!.ToString()!, (IEnumerable<string>)knownIds));
    }
}
