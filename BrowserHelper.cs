using System.Web;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System;
using Microsoft.AspNetCore.Hosting;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using System.IO;
using Microsoft.AspNetCore.Http;
using System.Threading;
using System.Net;
using System.Net.Sockets;

// https://brockallen.com/2016/09/24/process-start-for-urls-on-net-core/
// Kestrel Server help from Gary Archer https://stackoverflow.com/a/67821497/2272235
public class BrowserHelper
{
    public static int GetRandomUnusedPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    public static Process OpenBrowser(string url)
    {
        try
        {
            return Process.Start(url);
        }
        catch
        {
            // hack because of this: https://github.com/dotnet/corefx/issues/10361
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                url = url.Replace("&", "^&");
                return Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return Process.Start("xdg-open", url);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return Process.Start("open", url);
            }
            else
            {
                throw;
            }
        }
    }

    public int Port { get; }
    readonly string _path;

    public BrowserHelper(string path, int port)
    {
        Port = port;
        _path = path;
    }

    public async Task<string> GetAuthTokenAsync(string initialUrl, string expectedState = null)
    {
        await using var listener = new LoopbackHttpListener(Port, _path, expectedState);

        var browserProcess = BrowserHelper.OpenBrowser(initialUrl);

        try 
        {
            var result = await listener.WaitForCallbackAsync();
            if (string.IsNullOrWhiteSpace(result))
            {
                throw new Exception("Unknown error: Empty response");
            }

            browserProcess?.Close();

            return result;
        }
        catch (TaskCanceledException)
        {
            throw new TimeoutException();
        }
        catch (Exception)
        {
            throw;
        }
    }
}

public class LoopbackHttpListener : IAsyncDisposable
{
    const int _DEFAULT_TIMEOUT = 60 * 5; // 5 minutes

    IWebHost _host;
    readonly TaskCompletionSource<string> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    string _url;
    readonly PathString _callbackPath;
    readonly string _expectedState;

    public LoopbackHttpListener(int port, string path = null, string expectedState = null)
    {
        _expectedState = expectedState;
        path = path ?? string.Empty;
        if (path.StartsWith("/")) { path = path.Substring(1); }
        _callbackPath = string.IsNullOrWhiteSpace(path) ? PathString.Empty : new PathString("/" + path);

        _url = $"http://127.0.0.1:{port}";

        _host = new WebHostBuilder()
            .UseKestrel()
            .UseUrls(_url)
            .Configure(Configure)
            .Build();

        _host.Start();
    }

    public async ValueTask DisposeAsync()
    {
        await Task.Delay(500);
        _host.Dispose();
    }

    void Configure(IApplicationBuilder builder)
    {
        builder.Run(async context =>
        {
            if (context.Request.Method == "GET" && IsExpectedCallbackPath(context.Request.Path))
            {
                await SetResult(context.Request.QueryString.Value, context);
            }
            else
            {
                context.Response.StatusCode = 405;
                context.Response.ContentType = "text/html";
                await context.Response.WriteAsync("<HTML><BODY><H1>Error</H1><p>You have entered an incorrect address.</p></BODY></HTML>");
                context.Response.Body.Flush();
            }
        });
    }

    bool IsExpectedCallbackPath(PathString requestPath)
        => _callbackPath == PathString.Empty || requestPath.Equals(_callbackPath, StringComparison.OrdinalIgnoreCase);

    async Task SetResult(string value, HttpContext context)
    {
        context.Response.ContentType = "text/html";
        try
        {
            var queryStrings = HttpUtility.ParseQueryString(value);

            if (_expectedState != null && !string.Equals(queryStrings["state"], _expectedState, StringComparison.Ordinal))
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("Invalid sign-in state. Return to the browser sign-in started by GitPrune.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(queryStrings["error"]))
            {
                await context.Response.WriteAsync("Sign-in was declined. You can return to GitPrune.");
                _tcs.TrySetException(new InvalidOperationException("GitHub sign-in was declined. Run gprune --login to try again."));
                return;
            }

            var code = queryStrings["code"];

            if (string.IsNullOrWhiteSpace(code))
            {
                return; // This isn't our call
            }

            await context.Response.WriteAsync("<h2>You can now return to the application.</h2>");
            _tcs.TrySetResult(code);
        }
        catch
        {
            return; // This isn't our call
        }
    }

    public Task<string> WaitForCallbackAsync(int timeoutInSeconds = _DEFAULT_TIMEOUT)
    {
        return _tcs.Task.WaitAsync(TimeSpan.FromSeconds(timeoutInSeconds));
    }
}
