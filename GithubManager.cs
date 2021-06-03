using System;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Octokit;

public class GithubManager
{
    public GithubManager(Settings settings)
    {
        _client = new GitHubClient(new ProductHeaderValue("GitPrune"));
        _settings = settings;
    }

    readonly GitHubClient _client;
    readonly Settings _settings;

    bool _hasSetCredentials = false;

    public async Task<PullRequest> FindClosedPullRequestFromBranchName(string branchName)
    {
        if (!_hasSetCredentials)
        {
            throw new CredentialsNotSetException();
        }

        var pullRequest = await _client.PullRequest.GetAllForRepository(_settings.GithubOwner, _settings.GithubRepo, new PullRequestRequest() {
            State = ItemStateFilter.Closed,
            Head = $"{_settings.GithubOwner}:{branchName}",
        });

        return pullRequest?.FirstOrDefault();
    }

    public async Task SetCredentialsAsync(bool forceReLogin = false)
    {
        var token = SettingsManager.GetOauthToken()?.OAuthToken;

        if (forceReLogin || string.IsNullOrWhiteSpace(token))
        {
            token = await _GetOauthToken();
        }

        _client.Credentials = new(token);
        _hasSetCredentials = true;
    }

    async Task<string> _GetOauthToken()
    {
        var oauthUrl = _client.Oauth.GetGitHubLoginUrl(
            new OauthLoginRequest(Secrets.ClientId)
            {
                Scopes = { "repo", "read:org", "read:user" },
            });

        var browser = new BrowserHelper("", 58292);
        var code = await browser.GetAuthTokenAsync(oauthUrl.AbsoluteUri);

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new Exception("An error occured: code was blank.");
        }

        var oauthToken = await _client.Oauth.CreateAccessToken(new OauthTokenRequest(clientId: Secrets.ClientId, clientSecret: Secrets.ClientSecret, code: code));

        try
        {
            SettingsManager.SaveOauthToken(oauthToken.AccessToken);
        }
        catch {}

        return oauthToken.AccessToken;
    }

    [Serializable]
    private class CredentialsNotSetException : Exception
    {
        public CredentialsNotSetException()
        {
        }

        public CredentialsNotSetException(string message) : base(message)
        {
        }

        public CredentialsNotSetException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected CredentialsNotSetException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}