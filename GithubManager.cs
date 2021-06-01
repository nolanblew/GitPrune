using System.Linq;
using System.Threading.Tasks;
using Octokit;

public class GithubManager
{
    public GithubManager(Settings settings)
    {
        _client = new GitHubClient(new ProductHeaderValue("GitPrune"));
        _client.Credentials = new(settings.GithubToken);
        _settings = settings;
    }

    readonly GitHubClient _client;
    readonly Settings _settings;

    public async Task<PullRequest> FindClosedPullRequestFromBranchName(string branchName)
    {
        var pullRequest = await _client.PullRequest.GetAllForRepository(_settings.GithubOwner, _settings.GithubRepo, new PullRequestRequest() {
            State = ItemStateFilter.Closed,
            Head = $"{_settings.GithubOwner}:{branchName}",
        });

        return pullRequest?.FirstOrDefault();
    }
}