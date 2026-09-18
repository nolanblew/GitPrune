using System;

public static class PruneEligibility
{
    // A reused branch name is not evidence that its current commits were merged.
    public static bool MatchesMergedHead(string localHead, string pullRequestHead, bool merged)
        => merged && !string.IsNullOrWhiteSpace(localHead)
            && !string.IsNullOrWhiteSpace(pullRequestHead)
            && string.Equals(localHead, pullRequestHead, StringComparison.OrdinalIgnoreCase);
}
