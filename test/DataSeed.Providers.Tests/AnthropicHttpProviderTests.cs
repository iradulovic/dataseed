using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DataSeed.Providers;
using Xunit;

namespace DataSeed.Providers.Tests;

public class AnthropicHttpProviderTests
{
    private static HttpClient MakeClient(string responseJson, HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new MockHttpHandler(responseJson, status);
        return new HttpClient(handler);
    }

    [Fact]
    public async Task Sends_correct_headers_and_returns_text()
    {
        var body = JsonSerializer.Serialize(new
        {
            content = new[] { new { text = "[{\"id\":\"sup-1\"}]" } }
        });
        var provider = new AnthropicHttpProvider(MakeClient(body), "test-key", null);
        var result = await provider.CompleteAsync("prompt");
        Assert.Equal("[{\"id\":\"sup-1\"}]", result);
    }

    [Fact]
    public async Task Throws_on_non_success_status()
    {
        var provider = new AnthropicHttpProvider(
            MakeClient("{\"error\":\"bad\"}", HttpStatusCode.Unauthorized), "key", null);
        await Assert.ThrowsAsync<HttpRequestException>(() => provider.CompleteAsync("p"));
    }
}

public class OpenAiHttpProviderTests
{
    private static HttpClient MakeClient(string responseJson)
    {
        var handler = new MockHttpHandler(responseJson, HttpStatusCode.OK);
        return new HttpClient(handler);
    }

    [Fact]
    public async Task Returns_message_content()
    {
        var body = JsonSerializer.Serialize(new
        {
            choices = new[] { new { message = new { content = "[{\"id\":\"p-1\"}]" } } }
        });
        var provider = new OpenAiHttpProvider(MakeClient(body), "test-key", null);
        var result = await provider.CompleteAsync("prompt");
        Assert.Equal("[{\"id\":\"p-1\"}]", result);
    }
}

internal class MockHttpHandler : HttpMessageHandler
{
    private readonly string _response;
    private readonly HttpStatusCode _status;

    public MockHttpHandler(string response, HttpStatusCode status)
    {
        _response = response;
        _status = status;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var msg = new HttpResponseMessage(_status)
        {
            Content = new StringContent(_response, Encoding.UTF8, "application/json")
        };
        return Task.FromResult(msg);
    }
}
