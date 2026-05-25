using System;
using System.Net.Http;
using DataSeed.Engine;

namespace DataSeed.Providers;

public class ProviderFactory
{
    private readonly HttpClient _http;

    public ProviderFactory(HttpClient http) => _http = http;

    public ILlmProvider Create(string providerName, string? apiKey = null, string? model = null)
    {
        ILlmProvider inner = providerName?.ToLowerInvariant() switch
        {
            "anthropic" => new AnthropicHttpProvider(_http, apiKey, model),
            "openai" => new OpenAiHttpProvider(_http, apiKey, model),
            "claude-code" => new ClaudeCodeCliProvider(),
            "codex" => new CodexCliProvider(),
            _ => throw new ArgumentException($"Unknown provider '{providerName}'. Valid values: anthropic, openai, claude-code, codex.")
        };

        return new LlmRetryWrapper(inner);
    }
}
