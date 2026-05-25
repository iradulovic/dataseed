using System;
using System.Collections.Generic;
using System.Linq;
using DataSeed.Schema.Models;

namespace DataSeed.Engine;

public class DependencyGraph
{
    public static List<EntityDefinition> TopologicalSort(List<EntityDefinition> entities)
    {
        var byName = entities.ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);
        var inDegree = entities.ToDictionary(e => e.Name, _ => 0, StringComparer.OrdinalIgnoreCase);
        var edges = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var e in entities)
            edges[e.Name] = new List<string>();

        foreach (var entity in entities)
        {
            // parent → entity edge
            if (entity.Parent is not null && byName.ContainsKey(entity.Parent))
            {
                edges[entity.Parent].Add(entity.Name);
                inDegree[entity.Name]++;
            }

            // ref property → entity edge
            foreach (var prop in entity.Properties)
            {
                if (prop.Ref is not null && byName.ContainsKey(prop.Ref))
                {
                    edges[prop.Ref].Add(entity.Name);
                    inDegree[entity.Name]++;
                }
            }
        }

        // Kahn's algorithm
        var queue = new Queue<string>(
            inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        var sorted = new List<EntityDefinition>();

        while (queue.Count > 0)
        {
            var name = queue.Dequeue();
            sorted.Add(byName[name]);

            foreach (var neighbor in edges[name])
            {
                inDegree[neighbor]--;
                if (inDegree[neighbor] == 0)
                    queue.Enqueue(neighbor);
            }
        }

        if (sorted.Count != entities.Count)
            throw new InvalidOperationException("Circular dependency detected in entity definitions.");

        return sorted;
    }
}
