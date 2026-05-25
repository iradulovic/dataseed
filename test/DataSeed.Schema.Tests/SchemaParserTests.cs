using DataSeed.Schema;
using DataSeed.Schema.Models;
using Xunit;

namespace DataSeed.Schema.Tests;

public class SchemaParserTests
{
    private readonly SchemaParser _parser = new();

    private const string MinimalYaml = """
        domain: Test Domain
        description: A test domain
        entities:
          - name: Widget
            type: reference
            count: 5
            description: A widget
            properties:
              - name: name
                description: Widget name
        """;

    [Fact]
    public void Parses_domain_and_description()
    {
        var schema = _parser.ParseYaml(MinimalYaml);
        Assert.Equal("Test Domain", schema.Domain);
        Assert.Equal("A test domain", schema.Description);
    }

    [Fact]
    public void Parses_entity_name_and_type()
    {
        var schema = _parser.ParseYaml(MinimalYaml);
        Assert.Single(schema.Entities);
        Assert.Equal("Widget", schema.Entities[0].Name);
        Assert.Equal(EntityType.Reference, schema.Entities[0].Type);
    }

    [Fact]
    public void Parses_entity_count()
    {
        var schema = _parser.ParseYaml(MinimalYaml);
        Assert.Equal(5, schema.Entities[0].Count);
    }

    [Fact]
    public void Parses_property_definitions()
    {
        var schema = _parser.ParseYaml(MinimalYaml);
        Assert.Single(schema.Entities[0].Properties);
        Assert.Equal("name", schema.Entities[0].Properties[0].Name);
    }

    [Fact]
    public void Parses_taxonomy_type()
    {
        var yaml = """
            domain: D
            description: d
            entities:
              - name: Cat
                type: taxonomy
                depth: 3
                separator: " > "
                mustInclude:
                  - A > B > C
            """;
        var schema = _parser.ParseYaml(yaml);
        Assert.Equal(EntityType.Taxonomy, schema.Entities[0].Type);
        Assert.Equal(3, schema.Entities[0].Depth);
        Assert.Single(schema.Entities[0].MustInclude);
    }

    [Fact]
    public void Parses_hints_as_strings()
    {
        var yaml = """
            domain: D
            description: d
            entities:
              - name: Product
                type: dynamic
                count: 10
                properties:
                  - name: sku
                    hints:
                      - unique
                      - derived: "PRD-{sequence:4}"
            """;
        var schema = _parser.ParseYaml(yaml);
        var hints = schema.Entities[0].Properties[0].Hints;
        Assert.Contains("unique", hints);
    }

    [Fact]
    public void Parses_quality_profile()
    {
        var yaml = """
            domain: D
            description: d
            entities:
              - name: Product
                type: dynamic
                count: 100
                qualityProfile:
                  gold: 60%
                  poor: 40%
                properties: []
            """;
        var schema = _parser.ParseYaml(yaml);
        var qp = schema.Entities[0].QualityProfile;
        Assert.Equal("60%", qp["gold"]);
        Assert.Equal("40%", qp["poor"]);
    }
}
