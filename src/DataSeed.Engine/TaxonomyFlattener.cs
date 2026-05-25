using System.Collections.Generic;
using DataSeed.Engine.Models;

namespace DataSeed.Engine;

public static class TaxonomyFlattener
{
    public static List<string> GetLeafPaths(List<TaxonomyNode> tree, string separator)
    {
        var result = new List<string>();
        foreach (var node in tree)
            CollectLeaves(node, string.Empty, separator, result);
        return result;
    }

    public static List<string> GetAllPaths(List<TaxonomyNode> tree, string separator)
    {
        var result = new List<string>();
        foreach (var node in tree)
            CollectAll(node, string.Empty, separator, result);
        return result;
    }

    public static List<string> GetPathsAtDepth(List<TaxonomyNode> tree, string separator, int depth)
    {
        var result = new List<string>();
        foreach (var node in tree)
            CollectAtDepth(node, string.Empty, separator, depth, 1, result);
        return result;
    }

    private static void CollectLeaves(TaxonomyNode node, string prefix, string sep, List<string> result)
    {
        var path = prefix.Length > 0 ? prefix + sep + node.Node : node.Node;
        if (node.Children == null || node.Children.Count == 0)
        {
            result.Add(path);
            return;
        }
        foreach (var child in node.Children)
            CollectLeaves(child, path, sep, result);
    }

    private static void CollectAll(TaxonomyNode node, string prefix, string sep, List<string> result)
    {
        var path = prefix.Length > 0 ? prefix + sep + node.Node : node.Node;
        result.Add(path);
        foreach (var child in node.Children ?? new())
            CollectAll(child, path, sep, result);
    }

    private static void CollectAtDepth(TaxonomyNode node, string prefix, string sep, int targetDepth, int currentDepth, List<string> result)
    {
        var path = prefix.Length > 0 ? prefix + sep + node.Node : node.Node;
        if (currentDepth == targetDepth) { result.Add(path); return; }
        foreach (var child in node.Children ?? new())
            CollectAtDepth(child, path, sep, targetDepth, currentDepth + 1, result);
    }
}
