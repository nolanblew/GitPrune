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

// https://brockallen.com/2016/09/24/process-start-for-urls-on-net-core/
// Kestrel Server help from Gary Archer https://stackoverflow.com/a/67821497/2272235
public class BrowserHelper
{
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

    public async Task<string> GetAuthTokenAsync(string initialUrl)
    {
        await using var listener = new LoopbackHttpListener(Port, _path);

        var browserProcess = BrowserHelper.OpenBrowser(initialUrl);

        try 
        {
            var result = await listener.WaitForCallbackAsync();
            if (string.IsNullOrWhiteSpace(result))
            {
                throw new Exception("Unknown error: Empty response");
            }

            browserProcess.Close();

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
    TaskCompletionSource<string> _tcs = new TaskCompletionSource<string>();
    string _url;

    public LoopbackHttpListener(int port, string path = null)
    {
        path = path ?? string.Empty;
        if (path.StartsWith("/")) { path = path.Substring(1); }

        _url = $"http://127.0.0.1:{port}/{path}";

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
            if (context.Request.Method == "GET")
            {
                SetResult(context.Request.QueryString.Value, context);
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

    void SetResult(string value, HttpContext context)
    {
        try
        {
            var queryStrings = HttpUtility.ParseQueryString(value);

            var code = queryStrings["code"];

            if (string.IsNullOrWhiteSpace(code))
            {
                return; // This isn't our call
            }

            _tcs.TrySetResult(code);
        }
        catch
        {
            return; // This isn't our call
        }


        try
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/html";
            context.Response.WriteAsync("<h2>You can now return to the application.</h2>");
            context.Response.Body.Flush();
        }
        catch
        {
            context.Response.StatusCode = 400;
            context.Response.ContentType = "text/html";
            context.Response.WriteAsync("<h2>Invalid Request</h2>");
            context.Response.Body.Flush();
        }
    }

    public Task<string> WaitForCallbackAsync(int timeoutInSeconds = _DEFAULT_TIMEOUT)
    {
        Task.Run(async () =>
        {
            await Task.Delay(timeoutInSeconds * 1000);
            _tcs.SetCanceled();
        });

        return _tcs.Task;
    }
}