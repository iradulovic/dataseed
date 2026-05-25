using System.IO;
using DataSeed.Engine.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DataSeed.Engine;

public class PlanSerializer
{
    private readonly ISerializer _serializer;
    private readonly IDeserializer _deserializer;

    public PlanSerializer()
    {
        _serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .DisableAliases()
            .Build();

        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    public void WriteToFile(PlanFile plan, string path)
    {
        var yaml = _serializer.Serialize(plan);
        File.WriteAllText(path, yaml);
    }

    public PlanFile ReadFromFile(string path)
    {
        var yaml = File.ReadAllText(path);
        return _deserializer.Deserialize<PlanFile>(yaml);
    }
}
