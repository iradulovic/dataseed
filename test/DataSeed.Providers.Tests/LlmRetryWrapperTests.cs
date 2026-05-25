using System;
using System.Threading;
using System.Threading.Tasks;
using DataSeed.Engine;
using DataSeed.Providers;
using Moq;
using Xunit;

namespace DataSeed.Providers.Tests;

public class LlmRetryWrapperTests
{
    [Fact]
    public async Task Returns_valid_json_on_first_attempt()
    {
        var inner = new Mock<ILlmProvider>();
        inner.Setup(p => p.CompleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync("[{\"id\":\"1\"}]");

        var wrapper = new LlmRetryWrapper(inner.Object);
        var result = await wrapper.CompleteAsync("test");

        Assert.Equal("[{\"id\":\"1\"}]", result);
        inner.Verify(p => p.CompleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Retries_on_invalid_json_and_succeeds()
    {
        var inner = new Mock<ILlmProvider>();
        var callCount = 0;
        inner.Setup(p => p.CompleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(() =>
             {
                 callCount++;
                 return callCount < 2 ? "not json" : "[{}]";
             });

        var wrapper = new LlmRetryWrapper(inner.Object);
        var result = await wrapper.CompleteAsync("test");

        Assert.Equal("[{}]", result);
        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task Throws_after_three_failures()
    {
        var inner = new Mock<ILlmProvider>();
        inner.Setup(p => p.CompleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync("not valid json at all !!!");

        var wrapper = new LlmRetryWrapper(inner.Object);
        await Assert.ThrowsAsync<InvalidOperationException>(() => wrapper.CompleteAsync("test"));
        inner.Verify(p => p.CompleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Theory]
    [InlineData("```json\n[{}]\n```", "[{}]")]
    [InlineData("```\n[{}]\n```", "[{}]")]
    [InlineData("[{}]", "[{}]")]
    public void StripCodeFences_removes_fences(string input, string expected)
    {
        var result = LlmRetryWrapper.StripCodeFences(input);
        Assert.Equal(expected, result);
    }
}
