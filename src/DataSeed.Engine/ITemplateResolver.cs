using System;
using System.Collections.Generic;

namespace DataSeed.Engine;

public interface ITemplateResolver
{
    string Resolve(
        string template,
        IReadOnlyDictionary<string, IReadOnlyList<string>> parts,
        Random rng);
}
