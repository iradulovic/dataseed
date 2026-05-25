using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DataSeed.Engine;

namespace DataSeed.Providers;

public class OpenAiHttpProvider : ILlmProvider
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _model;

    public OpenAiHttpProvider(HttpClient http, string? apiKey, string? model)
    {
        _http = http;
        _apiKey = apiKey ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            ?? throw new InvalidOperationException("OpenAI API key not provided. Set --api-key or OPENAI_API_KEY.");
        _model = model ?? "gpt-4o";
    }

    public async Task<string> CompleteAsync(string prompt, CancellationToken ct = default)
    {
        var body = JsonSerializer.Serialize(new
        {
            model = _model,
            messages = new[] { new { role = "user", content = prompt } }
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        using var response = await _http.SendAsync(request, ct);
        var json = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"OpenAI API error {(int)response.StatusCode}: {json}");

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement
                   .GetProperty("choices")[0]
                   .GetProperty("message")
                   .GetProperty("content")
                   .GetString()
               ?? throw new InvalidOperationException("OpenAI response missing content.");
    }
}
