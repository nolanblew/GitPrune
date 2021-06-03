public interface ISecrets
{
    /// <summary>
    /// The ClientId for GitHub
    /// </summary>
    static string ClientId { get; }

    /// <summary>
    /// The ClientSecret for GitHub
    /// </summary>
    static string ClientSecret { get; }
}