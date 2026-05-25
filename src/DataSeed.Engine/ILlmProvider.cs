using System.Threading;
using System.Threading.Tasks;

namespace DataSeed.Engine;

public interface ILlmProvider
{
    Task<string> CompleteAsync(string prompt, CancellationToken ct = default);
}
