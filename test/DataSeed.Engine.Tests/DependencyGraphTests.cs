using System.Collections.Generic;
using System.Linq;
using DataSeed.Engine;
using DataSeed.Schema.Models;
using Xunit;

namespace DataSeed.Engine.Tests;

public class DependencyGraphTests
{
    [Fact]
    public void Reference_before_dynamic_that_refs_it()
    {
        var entities = new List<EntityDefinition>
        {
            new() { Name = "Product", Type = EntityType.Dynamic, Properties =
                [new PropertyDefinition { Name = "supplierId", Ref = "Supplier" }] },
            new() { Name = "Supplier", Type = EntityType.Reference }
        };

        var sorted = DependencyGraph.TopologicalSort(entities);
        var supplierIdx = sorted.FindIndex(e => e.Name == "Supplier");
        var productIdx = sorted.FindIndex(e => e.Name == "Product");
        Assert.True(supplierIdx < productIdx);
    }

    [Fact]
    public void Parent_before_child()
    {
        var entities = new List<EntityDefinition>
        {
            new() { Name = "OrderLine", Type = EntityType.Dynamic, Parent = "Order" },
            new() { Name = "Order", Type = EntityType.Dynamic }
        };

        var sorted = DependencyGraph.TopologicalSort(entities);
        var orderIdx = sorted.FindIndex(e => e.Name == "Order");
        var lineIdx = sorted.FindIndex(e => e.Name == "OrderLine");
        Assert.True(orderIdx < lineIdx);
    }

    [Fact]
    public void Complex_order_reference_taxonomy_dynamic_child()
    {
        var entities = new List<EntityDefinition>
        {
            new() { Name = "TransactionLine", Type = EntityType.Dynamic, Parent = "Transaction",
                Properties = [new PropertyDefinition { Name = "productId", Ref = "Product" }] },
            new() { Name = "Transaction", Type = EntityType.Dynamic,
                Properties = [new PropertyDefinition { Name = "customerId", Ref = "Customer" }] },
            new() { Name = "Product", Type = EntityType.Dynamic,
                Properties = [new PropertyDefinition { Name = "catId", Ref = "Category" }] },
            new() { Name = "Category", Type = EntityType.Taxonomy },
            new() { Name = "Customer", Type = EntityType.Dynamic }
        };

        var sorted = DependencyGraph.TopologicalSort(entities);
        var names = sorted.Select(e => e.Name).ToList();

        // Category must be before Product
        Assert.True(names.IndexOf("Category") < names.IndexOf("Product"));
        // Transaction must be before TransactionLine
        Assert.True(names.IndexOf("Transaction") < names.IndexOf("TransactionLine"));
    }

    [Fact]
    public void Throws_on_circular_dependency()
    {
        var entities = new List<EntityDefinition>
        {
            new() { Name = "A", Type = EntityType.Dynamic,
                Properties = [new PropertyDefinition { Name = "bId", Ref = "B" }] },
            new() { Name = "B", Type = EntityType.Dynamic,
                Properties = [new PropertyDefinition { Name = "aId", Ref = "A" }] }
        };

        Assert.Throws<System.InvalidOperationException>(
            () => DependencyGraph.TopologicalSort(entities));
    }
}
