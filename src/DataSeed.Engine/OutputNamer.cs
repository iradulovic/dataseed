using System;

namespace DataSeed.Engine;

public static class OutputNamer
{
    private static readonly string[] Adjectives =
    [
        "rusty", "hollow", "velvet", "amber", "silver", "crimson", "cobalt", "sandy", "mossy",
        "frozen", "blazing", "dusty", "gentle", "ancient", "silent", "bold", "calm", "dark",
        "eager", "faint", "grand", "hasty", "ideal", "jolly", "keen", "lofty", "misty",
        "noble", "olive", "proud", "quiet", "rapid", "sharp", "tawny", "urban", "vivid",
        "warm", "xenial", "young", "zippy", "brisk", "cedar", "denim", "ember", "flint",
        "gloom", "hazel", "ivory", "jade", "khaki"
    ];

    private static readonly string[] Animals =
    [
        "narwhal", "magpie", "capybara", "fennec", "quokka", "axolotl", "tapir", "ibis",
        "caracal", "numbat", "pangolin", "okapi", "dugong", "kinkajou", "margay", "serval",
        "binturong", "coati", "degu", "fossa", "genet", "hyrax", "jerboa", "kookaburra",
        "langur", "marmot", "nilgai", "ocelot", "peccary", "quoll", "rhebok", "saola",
        "takin", "urial", "vicuna", "wallaroo", "xerus", "yak", "zorilla", "aardwolf",
        "bongo", "civet", "dhole", "eland", "feral", "gaur", "hoopoe", "impala", "jacamar"
    ];

    public static string Generate()
    {
        var rng = new Random();
        var adj = Adjectives[rng.Next(Adjectives.Length)];
        var animal = Animals[rng.Next(Animals.Length)];
        var hex = Guid.NewGuid().ToString("N")[..4];
        return $"{adj}-{animal}-{hex}";
    }
}
