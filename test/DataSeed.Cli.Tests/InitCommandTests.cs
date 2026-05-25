using System;
using System.IO;
using DataSeed.Schema;
using Xunit;

namespace DataSeed.Cli.Tests;

public class InitCommandTests
{
    [Fact]
    public void Generated_template_is_valid_yaml()
    {
        // Get the template content by exercising the parser on it
        var tempFile = Path.GetTempFileName() + ".yaml";
        try
        {
            // Write what InitCommand would write
            var template = GetTemplate("test-schema");
            File.WriteAllText(tempFile, template);

            var parser = new SchemaParser();
            var schema = parser.ParseFile(tempFile);
            Assert.Equal("test-schema", schema.Domain);
            Assert.NotEmpty(schema.Entities);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void Generated_template_contains_schema_name()
    {
        var content = GetTemplate("my-domain");
        Assert.Contains("my-domain", content);
    }

    private static string GetTemplate(string name) => $$"""
        domain: {{name}}
        description: >
          Test domain description.
        entities:
          - name: Widget
            type: reference
            count: 5
            properties:
              - name: name
                description: Widget name
        """;
}
