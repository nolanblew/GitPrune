using System;
using System.Net;
using System.Threading.Tasks;
using Octokit;
using Xunit;

public class AuthenticationRecoveryTests
{
    [Fact]
    public async Task RetriesBatchOnceAndDoesNotAuthenticateAgain()
    {
        var recovery = new AuthenticationRecovery();
        int requests = 0, logins = 0, logs = 0;
        var result = await recovery.ExecuteAsync(
            () => ++requests == 1 ? Task.FromException<int>(new AuthorizationException()) : Task.FromResult(42),
            () => { logins++; return Task.CompletedTask; },
            _ => logs++);
        Assert.Equal(42, result);
        Assert.Equal(2, requests);
        Assert.Equal(1, logins);
        Assert.Equal(1, logs);
        await Assert.ThrowsAsync<AuthorizationException>(() => recovery.ExecuteAsync(
            () => Task.FromException<int>(new AuthorizationException()),
            () => { logins++; return Task.CompletedTask; }, _ => { }));
        Assert.Equal(1, logins);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task AccessErrorsDoNotTriggerLogin(HttpStatusCode status)
    {
        int logins = 0;
        await Assert.ThrowsAsync<ApiException>(() => new AuthenticationRecovery().ExecuteAsync(
            () => Task.FromException<int>(new ApiException("Access denied", status)),
            () => { logins++; return Task.CompletedTask; }, _ => { }));
        Assert.Equal(0, logins);
    }

    [Fact]
    public async Task FailedLoginStopsWithoutRetryingRequest()
    {
        int requests = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(() => new AuthenticationRecovery().ExecuteAsync(
            () => { requests++; return Task.FromException<int>(new AuthorizationException()); },
            () => throw new InvalidOperationException("Login cancelled"), _ => { }));
        Assert.Equal(1, requests);
    }
}
