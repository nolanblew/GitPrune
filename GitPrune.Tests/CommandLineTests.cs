using System;
using System.IO;
using Xunit;

public class CommandLineTests
{
    [Fact]
    public void OptionsCanPrecedeRepositoryDirectory()
        => Assert.Equal(Path.GetFullPath(Path.GetTempPath()),
            CommandLine.GetRepositoryDirectory(new[] { "-i", Path.GetTempPath() }));

    [Theory]
    [InlineData("--unknown")]
    [InlineData("/nonexistent-gitprune-test-directory")]
    public void InvalidArgumentsDoNotFallBackToCurrentDirectory(string argument)
        => Assert.Throws<ArgumentException>(() => CommandLine.GetRepositoryDirectory(new[] { argument }));

    [Fact]
    public void MultipleDirectoriesAreRejected()
        => Assert.Throws<ArgumentException>(() => CommandLine.GetRepositoryDirectory(
            new[] { Path.GetTempPath(), Path.GetTempPath() }));
}
