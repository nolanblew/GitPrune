using Xunit;

public class PruneEligibilityTests
{
    [Theory]
    [InlineData("abc", "abc", true, true)]
    [InlineData("abc", "def", true, false)]
    [InlineData("abc", "abc", false, false)]
    [InlineData(null, "abc", true, false)]
    [InlineData("abc", null, true, false)]
    public void OnlyTheExactMergedHeadIsEligible(string local, string remote, bool merged, bool expected)
        => Assert.Equal(expected, PruneEligibility.MatchesMergedHead(local, remote, merged));
}
