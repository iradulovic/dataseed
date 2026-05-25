using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DataSeed.Engine;

namespace DataSeed.Providers;

public class AnthropicHttpProvider : ILlmProvider
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _model;

    public AnthropicHttpProvider(HttpClient http, string? apiKey, string? model)
    {
        _http = http;
        _apiKey = apiKey ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
            ?? throw new InvalidOperationException("Anthropic API key not provided. Set --api-key or ANTHROPIC_API_KEY.");
        _model = model ?? "claude-opus-4-5";
    }

    public async Task<string> CompleteAsync(string prompt, CancellationToken ct = default)
    {
        var body = JsonSerializer.Serialize(new
        {
            model = _model,
            max_tokens = 8192,
            messages = new[] { new { role = "user", content = prompt } }
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        request.Headers.Add("x-api-key", _apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        using var response = await _http.SendAsync(request, ct);
        var json = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Anthropic API error {(int)response.StatusCode}: {json}");

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString()
               ?? throw new InvalidOperationException("Anthropic response missing text content.");
    }
}
