using System;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DataSeed.Engine;

namespace DataSeed.Providers;

public class LlmRetryWrapper : ILlmProvider
{
    private readonly ILlmProvider _inner;
    private const int MaxAttempts = 3;

    public LlmRetryWrapper(ILlmProvider inner) => _inner = inner;

    public async Task<string> CompleteAsync(string prompt, CancellationToken ct = default)
    {
        Exception? last = null;
        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            if (attempt > 0)
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)), ct);

            try
            {
                var raw = await _inner.CompleteAsync(prompt, ct);
                var cleaned = StripCodeFences(raw);
                JsonDocument.Parse(cleaned);
                return cleaned;
            }
            catch (JsonException ex)
            {
                last = ex;
            }
        }

        throw new InvalidOperationException(
            $"LLM provider failed to return valid JSON after {MaxAttempts} attempts.", last);
    }

    internal static string StripCodeFences(string text)
    {
        var trimmed = text.Trim();
        var m = Regex.Match(trimmed, @"^```(?:json)?\s*\n?([\s\S]*?)\n?```$", RegexOptions.Multiline);
        return m.Success ? m.Groups[1].Value.Trim() : trimmed;
    }
}
