using System;
using System.Collections.Generic;

namespace DataSeed.Engine.Models;

public class PlanFile
{
    public string Domain { get; set; } = string.Empty;
    public string SchemaFile { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public string Provider { get; set; } = string.Empty;
    public int Seed { get; set; }
    public Dictionary<string, EntityPlan> Entities { get; set; } = new();
}

public class EntityPlan
{
    public string Type { get; set; } = string.Empty;

    // For reference entities
    public List<Dictionary<string, object?>> Resolved { get; set; } = new();

    // For taxonomy entities
    public string? Separator { get; set; }
    public List<TaxonomyNode> ResolvedTree { get; set; } = new();
    public Dictionary<string, double> Weights { get; set; } = new();

    // For dynamic entities
    public int Count { get; set; }
    public Dictionary<string, PropertyStrategy> PropertyStrategies { get; set; } = new();
}

public class TaxonomyNode
{
    public string Node { get; set; } = string.Empty;
    public List<TaxonomyNode> Children { get; set; } = new();
}

public class PropertyStrategy
{
    public string? Bogus { get; set; }
    public string? Template { get; set; }
    public int? DegradePercent { get; set; }
    public string? DegradeStrategy { get; set; }
    public int? NullPercent { get; set; }
    public string? Distribution { get; set; }
    public double? Skew { get; set; }
    public List<string> Values { get; set; } = new();
    public (int Min, int Max)? Range { get; set; }
    public (int Min, int Max)? LinesPerParent { get; set; }
}
