using System;
using System.Net;
using System.Threading.Tasks;
using Octokit;

// Wrap an entire batch so concurrent 401 responses open only one browser login.
public sealed class AuthenticationRecovery
{
    bool attempted;

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, Func<Task> authenticate, Action<Exception> log)
    {
        try
        {
            return await operation();
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized && !attempted)
        {
            attempted = true;
            log(ex);
            await authenticate();
            return await operation();
        }
    }
}
