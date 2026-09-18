using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

public class TerminalProgressTests
{
    [Fact]
    public void FormatLine_ShowsProgressStatusAndTruncatesToTerminalWidth()
    {
        var line = TerminalProgress.FormatLine(
            "⠋",
            "Removing worktrees",
            completed: 3,
            total: 5,
            item: "/a/very/long/path/to/a/linked/worktree",
            elapsed: TimeSpan.FromSeconds(12.4),
            width: 72);

        Assert.Contains("3/5", line);
        Assert.Contains("60%", line);
        Assert.Contains("12.4s", line);
        Assert.True(line.Length <= 72, $"Progress line was {line.Length} characters: {line}");
    }

    [Fact]
    public async Task RunAsync_WhenOutputIsRedirected_WritesStableStartAndCompletionLines()
    {
        var output = new StringWriter();
        var progress = new TerminalProgress(output, interactive: false, () => 100);

        var result = await progress.RunAsync(
            "Deleting branches",
            completed: 0,
            total: 2,
            item: "feature/one",
            operation: () => Task.FromResult(true),
            succeeded: value => value);

        Assert.True(result);
        Assert.Contains("Deleting branches [1/2]: feature/one", output.ToString());
        Assert.Contains("✓ Deleting branches", output.ToString());
        Assert.DoesNotContain("\r", output.ToString());
    }

    [Fact]
    public async Task RunAsync_WhenInteractive_RefreshesTheLineWhileWorkIsRunning()
    {
        var output = new StringWriter();
        var progress = new TerminalProgress(output, interactive: true, () => 100);

        await progress.RunAsync(
            "Removing worktrees",
            completed: 0,
            total: 1,
            item: "/repos/slow-worktree",
            operation: async () =>
            {
                await Task.Delay(150);
                return true;
            },
            succeeded: value => value);

        Assert.Contains("\r\u001b[2K", output.ToString());
        Assert.Contains("0/1", output.ToString());
        Assert.Contains("✓ Removing worktrees", output.ToString());
        Assert.Contains("1/1", output.ToString());
    }
}
