using System.Text.RegularExpressions;
using DataSeed.Engine;
using Xunit;

namespace DataSeed.Engine.Tests;

public class OutputNamerTests
{
    [Fact]
    public void Generated_name_matches_pattern()
    {
        var name = OutputNamer.Generate();
        Assert.Matches(@"^[a-z]+-[a-z]+-[0-9a-f]{4}$", name);
    }

    [Fact]
    public void Generated_names_are_different()
    {
        var a = OutputNamer.Generate();
        var b = OutputNamer.Generate();
        // Hex suffix alone ensures near-zero collision probability
        var hexA = a.Split('-')[2];
        var hexB = b.Split('-')[2];
        // Can't guarantee words differ, but hex almost certainly will
        Assert.True(a != b || hexA != hexB, "Should produce distinct names");
    }
}
