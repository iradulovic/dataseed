using System.Collections.Generic;

namespace DataSeed.Schema.Models;

public class DomainSchema
{
    public string Domain { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<EntityDefinition> Entities { get; set; } = new();
}

public enum EntityType
{
    Reference,
    Taxonomy,
    Dynamic
}

public class EntityDefinition
{
    public string Name { get; set; } = string.Empty;
    public EntityType Type { get; set; }
    public int? Count { get; set; }
    public string? Description { get; set; }
    public string? Parent { get; set; }
    public int? Depth { get; set; }
    public string? Separator { get; set; }
    public List<string> MustInclude { get; set; } = new();
    public List<string> Hints { get; set; } = new();
    public List<PropertyDefinition> Properties { get; set; } = new();
    public Dictionary<string, string> QualityProfile { get; set; } = new();
}

public class PropertyDefinition
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Ref { get; set; }
    public List<string> Examples { get; set; } = new();
    public List<string> Hints { get; set; } = new();
}
