public interface ISecrets
{
    /// <summary>
    /// Github personal access token
    /// </summary>
    static string GithubToken { get; }

    /// <summary>
    /// The owner of the repo to access (ie 'owner' in https://github.com/{owner}/{repo}/)
    /// </summary>
    static string GithubOwner { get; }

    /// <summary>
    /// The repo name to access (ie 'repo' in https://github.com/{owner}/{repo}/)
    /// </summary>
    static string GithubRepo { get; }
}