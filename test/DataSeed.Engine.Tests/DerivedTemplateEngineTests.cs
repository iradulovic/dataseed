using System.Collections.Generic;
using System.Text.RegularExpressions;
using DataSeed.Engine;
using Xunit;

namespace DataSeed.Engine.Tests;

public class DerivedTemplateEngineTests
{
    private static DerivedTemplateEngine Make(
        Dictionary<string, List<Dictionary<string, object?>>>? entities = null)
        => new(entities ?? new(), seed: 42);

    [Fact]
    public void Sequence_increments_and_pads()
    {
        var engine = Make();
        var first = engine.Evaluate("{sequence:5}");
        var second = engine.Evaluate("{sequence:5}");
        Assert.Equal("00001", first);
        Assert.Equal("00002", second);
    }

    [Fact]
    public void Sequence_respects_width()
    {
        var engine = Make();
        var result = engine.Evaluate("{sequence:3}");
        Assert.Equal("001", result);
    }

    [Fact]
    public void Ref_lookup_resolves_property()
    {
        var entities = new Dictionary<string, List<Dictionary<string, object?>>>
        {
            ["Supplier"] = [new Dictionary<string, object?> { ["code"] = "WWT" }]
        };
        var engine = new DerivedTemplateEngine(entities, seed: 1);
        var result = engine.Evaluate("{Supplier.code}-{sequence:4}");
        Assert.StartsWith("WWT-", result);
        Assert.EndsWith("0001", result);
    }

    [Fact]
    public void Missing_entity_ref_returns_empty_string()
    {
        var engine = Make();
        var result = engine.Evaluate("{NoSuch.prop}");
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Literal_text_preserved()
    {
        var engine = Make();
        var result = engine.Evaluate("PREFIX-{sequence:4}");
        Assert.StartsWith("PREFIX-", result);
    }
}
