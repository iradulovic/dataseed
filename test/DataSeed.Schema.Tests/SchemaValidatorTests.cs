using System.Collections.Generic;
using DataSeed.Schema;
using DataSeed.Schema.Models;
using Xunit;

namespace DataSeed.Schema.Tests;

public class SchemaValidatorTests
{
    private readonly SchemaValidator _validator = new();

    private static DomainSchema ValidSchema() => new()
    {
        Domain = "Test",
        Description = "Test domain",
        Entities =
        [
            new EntityDefinition { Name = "Supplier", Type = EntityType.Reference, Count = 5 },
            new EntityDefinition
            {
                Name = "Product",
                Type = EntityType.Dynamic,
                Count = 10,
                Properties =
                [
                    new PropertyDefinition { Name = "supplierId", Ref = "Supplier" }
                ]
            }
        ]
    };

    [Fact]
    public void Valid_schema_has_no_errors()
    {
        var errors = _validator.Validate(ValidSchema());
        Assert.Empty(errors);
    }

    [Fact]
    public void Missing_domain_is_error()
    {
        var schema = ValidSchema();
        schema.Domain = string.Empty;
        var errors = _validator.Validate(schema);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void Duplicate_entity_name_is_error()
    {
        var schema = ValidSchema();
        schema.Entities.Add(new EntityDefinition { Name = "Supplier", Type = EntityType.Dynamic });
        var errors = _validator.Validate(schema);
        Assert.Contains(errors, e => e.Message.Contains("Duplicate"));
    }

    [Fact]
    public void Unknown_ref_target_is_error_when_catalog_provided()
    {
        var schema = ValidSchema();
        schema.Entities[1].Properties[0].Ref = "NonExistent";
        // Pass an explicit known-names list so the validator can definitively flag the unknown ref
        var errors = _validator.Validate(schema, externalEntityNames: new[] { "Supplier" });
        Assert.Contains(errors, e => e.Message.Contains("unknown entity"));
    }

    [Fact]
    public void Ref_to_dynamic_entity_is_allowed()
    {
        var schema = ValidSchema();
        schema.Entities[1].Properties.Add(
            new PropertyDefinition { Name = "otherProductId", Ref = "Product" });
        var errors = _validator.Validate(schema);
        Assert.DoesNotContain(errors, e => e.Message.Contains("dynamic entity"));
    }

    [Fact]
    public void Unknown_parent_is_error()
    {
        var schema = ValidSchema();
        schema.Entities.Add(new EntityDefinition
        {
            Name = "Child",
            Type = EntityType.Dynamic,
            Parent = "NoSuchParent"
        });
        var errors = _validator.Validate(schema);
        Assert.Contains(errors, e => e.Message.Contains("unknown parent"));
    }

    [Fact]
    public void Taxonomy_without_depth_is_error()
    {
        var schema = ValidSchema();
        schema.Entities.Add(new EntityDefinition
        {
            Name = "Cat",
            Type = EntityType.Taxonomy,
            Depth = 0
        });
        var errors = _validator.Validate(schema);
        Assert.Contains(errors, e => e.Message.Contains("depth >= 1"));
    }

    [Fact]
    public void Quality_profile_over_100_percent_is_error()
    {
        var schema = ValidSchema();
        schema.Entities[1].QualityProfile = new Dictionary<string, string>
        {
            ["gold"] = "70%",
            ["silver"] = "50%"
        };
        var errors = _validator.Validate(schema);
        Assert.Contains(errors, e => e.Message.Contains("sum to"));
    }

    [Fact]
    public void StructuredTemplate_with_derived_is_error()
    {
        var schema = ValidSchema();
        schema.Entities[1].Properties.Add(new PropertyDefinition
        {
            Name = "label",
            Hints = ["structuredTemplate", "derived: \"{sequence:5}\""]
        });
        var errors = _validator.Validate(schema);
        Assert.Contains(errors, e => e.Message.Contains("incompatible with 'derived'"));
    }

    [Fact]
    public void StructuredTemplate_with_values_is_error()
    {
        var schema = ValidSchema();
        schema.Entities[1].Properties.Add(new PropertyDefinition
        {
            Name = "label",
            Hints = ["structuredTemplate", "values: [A, B, C]"]
        });
        var errors = _validator.Validate(schema);
        Assert.Contains(errors, e => e.Message.Contains("incompatible with 'values'"));
    }

    [Fact]
    public void StructuredTemplate_with_range_is_error()
    {
        var schema = ValidSchema();
        schema.Entities[1].Properties.Add(new PropertyDefinition
        {
            Name = "label",
            Hints = ["structuredTemplate", "range: 1-100"]
        });
        var errors = _validator.Validate(schema);
        Assert.Contains(errors, e => e.Message.Contains("incompatible with 'range'"));
    }

    [Fact]
    public void StructuredTemplate_alone_is_valid()
    {
        var schema = ValidSchema();
        schema.Entities[1].Properties.Add(new PropertyDefinition
        {
            Name = "companyName",
            Hints = ["structuredTemplate"]
        });
        var errors = _validator.Validate(schema);
        Assert.DoesNotContain(errors, e => e.Message.Contains("companyName"));
    }
}
