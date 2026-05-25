using System;
using System.Collections.Generic;
using DataSeed.Schema;
using Xunit;

namespace DataSeed.Schema.Tests;

public class HintParserTests
{
    [Fact]
    public void Unique_parsed_correctly()
    {
        var hints = HintParser.Parse(["unique"]);
        Assert.True(hints.Unique);
    }

    [Fact]
    public void Nullable_percent_parsed()
    {
        var hints = HintParser.Parse(["nullable: 15%"]);
        Assert.Equal(15, hints.NullablePercent);
    }

    [Fact]
    public void Degradable_percent_parsed()
    {
        var hints = HintParser.Parse(["degradable: 40%"]);
        Assert.Equal(40, hints.DegradablePercent);
    }

    [Fact]
    public void Derived_template_parsed()
    {
        var hints = HintParser.Parse(["derived: \"{supplier.code}-{sequence:5}\""]);
        Assert.Equal("{supplier.code}-{sequence:5}", hints.DerivedTemplate);
    }

    [Fact]
    public void Values_list_parsed()
    {
        var hints = HintParser.Parse(["values: [EA, FT, PK]"]);
        Assert.Equal(new[] { "EA", "FT", "PK" }, hints.Values);
    }

    [Fact]
    public void Range_parsed()
    {
        var hints = HintParser.Parse(["range: 5-2500"]);
        Assert.Equal((5, 2500), hints.Range);
    }

    [Fact]
    public void Distribution_parsed()
    {
        var hints = HintParser.Parse(["distribution: long-tail"]);
        Assert.Equal("long-tail", hints.Distribution);
    }

    [Fact]
    public void Skew_parsed()
    {
        var hints = HintParser.Parse(["skew: 0.7"]);
        Assert.Equal(0.7, hints.Skew!.Value, precision: 5);
    }

    [Fact]
    public void Depth_leaf_parsed()
    {
        var hints = HintParser.Parse(["depth: leaf"]);
        Assert.True(hints.DepthLeaf);
    }

    [Fact]
    public void Depth_numeric_parsed()
    {
        var hints = HintParser.Parse(["depth: 2"]);
        Assert.Equal(2, hints.DepthLevel);
    }

    [Fact]
    public void LinesPerParent_parsed()
    {
        var hints = HintParser.Parse(["linesPerParent: 1-8"]);
        Assert.Equal((1, 8), hints.LinesPerParent);
    }

    [Fact]
    public void DateRange_parsed()
    {
        var hints = HintParser.Parse(["dateRange: \"2023-01-01/2024-12-31\""]);
        Assert.NotNull(hints.DateRange);
        Assert.Equal(new DateTime(2023, 1, 1), hints.DateRange!.Value.Start);
        Assert.Equal(new DateTime(2024, 12, 31), hints.DateRange!.Value.End);
    }

    [Fact]
    public void Multiple_hints_parsed_together()
    {
        var hints = HintParser.Parse(["unique", "nullable: 10%", "distribution: random"]);
        Assert.True(hints.Unique);
        Assert.Equal(10, hints.NullablePercent);
        Assert.Equal("random", hints.Distribution);
    }

    [Fact]
    public void StructuredTemplate_plain_parsed()
    {
        var hints = HintParser.Parse(["structuredTemplate"]);
        Assert.True(hints.StructuredTemplate);
        Assert.Null(hints.StructuredTemplateRef);
    }

    [Fact]
    public void StructuredTemplate_with_ref_parsed()
    {
        var hints = HintParser.Parse(["structuredTemplate: ref=categoryPath"]);
        Assert.True(hints.StructuredTemplate);
        Assert.Equal("categoryPath", hints.StructuredTemplateRef);
    }

    [Fact]
    public void StructuredTemplate_compatible_with_nullable()
    {
        var hints = HintParser.Parse(["structuredTemplate", "nullable: 30%"]);
        Assert.True(hints.StructuredTemplate);
        Assert.Equal(30, hints.NullablePercent);
    }
}
