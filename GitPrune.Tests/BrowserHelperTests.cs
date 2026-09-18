using System;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

public class BrowserHelperTests
{
    [Fact]
    public async Task CallbackRejectsWrongStateWithoutCompletingLogin()
    {
        var port = BrowserHelper.GetRandomUnusedPort();
        await using var listener = new LoopbackHttpListener(port, "authorize", "expected-state");
        using var client = new HttpClient();
        var callback = listener.WaitForCallbackAsync();
        using var wrong = await client.GetAsync($"http://127.0.0.1:{port}/authorize?code=bad&state=wrong");
        Assert.Equal(400, (int)wrong.StatusCode);
        Assert.False(callback.IsCompleted);
        using var valid = await client.GetAsync($"http://127.0.0.1:{port}/authorize?code=good&state=expected-state");
        Assert.Equal("good", await callback);
    }

    [Fact]
    public async Task DeniedLoginFailsPromptly()
    {
        var port = BrowserHelper.GetRandomUnusedPort();
        await using var listener = new LoopbackHttpListener(port, "authorize", "expected-state");
        using var client = new HttpClient();
        var callback = listener.WaitForCallbackAsync();
        using var response = await client.GetAsync($"http://127.0.0.1:{port}/authorize?error=access_denied&state=expected-state");
        await Assert.ThrowsAsync<InvalidOperationException>(() => callback);
    }

    [Fact]
    public async Task MissingCallbackTimesOut()
    {
        await using var listener = new LoopbackHttpListener(BrowserHelper.GetRandomUnusedPort(), "authorize");
        await Assert.ThrowsAsync<TimeoutException>(() => listener.WaitForCallbackAsync(0));
    }

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
