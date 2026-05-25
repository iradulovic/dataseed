using System;
using System.Collections.Generic;
using DataSeed.Engine;
using Xunit;

namespace DataSeed.Engine.Tests;

public class TemplateResolverTests
{
    private readonly ITemplateResolver _resolver = new TemplateResolver();
    private readonly Random _rng = new(42);

    [Fact]
    public void Single_token_replaced()
    {
        var parts = new Dictionary<string, IReadOnlyList<string>>
        {
            ["size"] = new[] { "large" }
        };

        var result = _resolver.Resolve("{size} widget", parts, _rng);

        Assert.Equal("large widget", result);
    }

    [Fact]
    public void Multiple_tokens_replaced()
    {
        var parts = new Dictionary<string, IReadOnlyList<string>>
        {
            ["size"] = new[] { "1/2 in." },
            ["material"] = new[] { "brass" }
        };

        var result = _resolver.Resolve("{size} {material} valve", parts, _rng);

        Assert.Equal("1/2 in. brass valve", result);
    }

    [Fact]
    public void Same_token_twice_picks_independently()
    {
        var parts = new Dictionary<string, IReadOnlyList<string>>
        {
            ["connection"] = new[] { "FPT", "MPT", "push-fit" }
        };

        // Run many times; occasionally the two picks will differ
        var sawDifferent = false;
        var rng = new Random(1);
        for (int i = 0; i < 100; i++)
        {
            var result = _resolver.Resolve("{connection} x {connection}", parts, rng);
            var halves = result.Split(" x ");
            if (halves[0] != halves[1]) { sawDifferent = true; break; }
        }

        Assert.True(sawDifferent, "Expected at least one case where both tokens differ.");
    }

    [Fact]
    public void Unknown_token_left_unchanged()
    {
        var parts = new Dictionary<string, IReadOnlyList<string>>();

        var result = _resolver.Resolve("{unknown} token", parts, _rng);

        Assert.Equal("{unknown} token", result);
    }

    [Fact]
    public void Value_picked_from_list()
    {
        var parts = new Dictionary<string, IReadOnlyList<string>>
        {
            ["color"] = new[] { "red", "green", "blue" }
        };

        var result = _resolver.Resolve("{color}", parts, _rng);

        Assert.Contains(result, new[] { "red", "green", "blue" });
    }
}
