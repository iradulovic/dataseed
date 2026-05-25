using System;
using DataSeed.Engine;
using Xunit;

namespace DataSeed.Engine.Tests;

public class DegradationEngineTests
{
    private readonly Random _rng = new(42);

    [Fact]
    public void TruncateToNoun_keeps_first_few_words()
    {
        var result = DegradationEngine.Degrade(
            "High pressure commercial ball valve for industrial piping", "truncate-to-noun", _rng);
        var wordCount = result.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        Assert.InRange(wordCount, 1, 3);
    }

    [Fact]
    public void VagueSubstitute_returns_non_empty_string()
    {
        var result = DegradationEngine.Degrade(
            "Some original description", "vague-substitute", _rng);
        Assert.NotEmpty(result);
        Assert.NotEqual("Some original description", result);
    }

    [Fact]
    public void Unknown_strategy_falls_back_to_truncate()
    {
        var result = DegradationEngine.Degrade("Long product description here", "unknown", _rng);
        Assert.NotEmpty(result);
        var wordCount = result.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        Assert.InRange(wordCount, 1, 3);
    }

    [Fact]
    public void Empty_input_returned_unchanged()
    {
        var result = DegradationEngine.Degrade("", "truncate-to-noun", _rng);
        Assert.Equal("", result);
    }
}
