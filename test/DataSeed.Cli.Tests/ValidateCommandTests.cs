using System.IO;
using DataSeed.Schema;
using Xunit;

namespace DataSeed.Cli.Tests;

public class ValidateCommandTests
{
    private readonly SchemaParser _parser = new();
    private readonly SchemaValidator _validator = new();

    private const string ValidYaml = """
        domain: Test
        description: A test
        entities:
          - name: Widget
            type: reference
            count: 5
        """;

    private const string InvalidYaml = """
        domain: ""
        description: test
        entities: []
        """;

    [Fact]
    public void Valid_schema_produces_no_errors()
    {
        var schema = _parser.ParseYaml(ValidYaml);
        var errors = _validator.Validate(schema);
        Assert.Empty(errors);
    }

    [Fact]
    public void Invalid_schema_produces_errors()
    {
        var schema = _parser.ParseYaml(InvalidYaml);
        var errors = _validator.Validate(schema);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void Parser_throws_on_missing_file()
    {
        Assert.Throws<FileNotFoundException>(() => _parser.ParseFile("does-not-exist.yaml"));
    }
}
