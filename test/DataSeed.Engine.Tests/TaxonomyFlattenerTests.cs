using System.Collections.Generic;
using DataSeed.Engine;
using DataSeed.Engine.Models;
using Xunit;

namespace DataSeed.Engine.Tests;

public class TaxonomyFlattenerTests
{
    private static List<TaxonomyNode> SampleTree() =>
    [
        new TaxonomyNode
        {
            Node = "Plumbing",
            Children =
            [
                new TaxonomyNode
                {
                    Node = "Valves",
                    Children =
                    [
                        new TaxonomyNode { Node = "Ball Valves" },
                        new TaxonomyNode { Node = "Gate Valves" }
                    ]
                },
                new TaxonomyNode
                {
                    Node = "Pipe",
                    Children = [new TaxonomyNode { Node = "Copper Pipe" }]
                }
            ]
        }
    ];

    [Fact]
    public void GetLeafPaths_returns_correct_leaves()
    {
        var leaves = TaxonomyFlattener.GetLeafPaths(SampleTree(), " > ");
        Assert.Contains("Plumbing > Valves > Ball Valves", leaves);
        Assert.Contains("Plumbing > Valves > Gate Valves", leaves);
        Assert.Contains("Plumbing > Pipe > Copper Pipe", leaves);
        Assert.Equal(3, leaves.Count);
    }

    [Fact]
    public void GetAllPaths_includes_intermediate_nodes()
    {
        var all = TaxonomyFlattener.GetAllPaths(SampleTree(), " > ");
        Assert.Contains("Plumbing", all);
        Assert.Contains("Plumbing > Valves", all);
        Assert.Contains("Plumbing > Valves > Ball Valves", all);
    }

    [Fact]
    public void GetPathsAtDepth_returns_depth_2_nodes()
    {
        var depth2 = TaxonomyFlattener.GetPathsAtDepth(SampleTree(), " > ", 2);
        Assert.Contains("Plumbing > Valves", depth2);
        Assert.Contains("Plumbing > Pipe", depth2);
        Assert.DoesNotContain("Plumbing > Valves > Ball Valves", depth2);
    }

    [Fact]
    public void Separator_is_used_correctly()
    {
        var leaves = TaxonomyFlattener.GetLeafPaths(SampleTree(), "::");
        Assert.Contains("Plumbing::Valves::Ball Valves", leaves);
    }
}
