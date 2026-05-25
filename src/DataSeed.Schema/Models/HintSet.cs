using System;
using System.Collections.Generic;

namespace DataSeed.Schema.Models;

public class HintSet
{
    public bool Unique { get; set; }
    public int? NullablePercent { get; set; }
    public int? DegradablePercent { get; set; }
    public string? DerivedTemplate { get; set; }
    public List<string> Values { get; set; } = new();
    public (int Min, int Max)? Range { get; set; }
    public string? Distribution { get; set; }
    public double? Skew { get; set; }
    public int? DepthLevel { get; set; }
    public bool DepthLeaf { get; set; }
    public (int Min, int Max)? LinesPerParent { get; set; }
    public (DateTime Start, DateTime End)? DateRange { get; set; }
    public bool StructuredTemplate { get; set; }
    public string? StructuredTemplateRef { get; set; }
}
