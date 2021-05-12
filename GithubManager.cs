using System.Linq;
using System.Threading.Tasks;
using Octokit;

public class GithubManager
{
    public GithubManager()
    {
        _client = new GitHubClient(new ProductHeaderValue("GitPrune"));
        _client.Credentials = new(Secrets.GithubToken);
    }

    readonly GitHubClient _client;

    public async Task<PullRequest> FindClosedPullRequestFromBranchName(string branchName)
    {
        var pullRequest = await _client.PullRequest.GetAllForRepository(Secrets.GithubOwner, Secrets.GithubRepo, new PullRequestRequest() {
            State = ItemStateFilter.Closed,
            Head = $"{Secrets.GithubOwner}:{branchName}",
        });

        return pullRequest?.FirstOrDefault();
    }
}