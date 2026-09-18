using System;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

public class BrowserHelperTests
{
    [Fact]
    public async Task LoopbackListenerAcceptsTheExplicitAuthorizeCallbackPath()
    {
        var port = BrowserHelper.GetRandomUnusedPort();
        await using var listener = new LoopbackHttpListener(port, "authorize");
        using var httpClient = new HttpClient();

        var callbackTask = listener.WaitForCallbackAsync();
        using var response = await httpClient.GetAsync($"http://127.0.0.1:{port}/authorize?code=test-code");

        Assert.Equal(200, (int)response.StatusCode);
        Assert.Equal("test-code", await callbackTask);
    }
}
