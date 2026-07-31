using System;
using System.Diagnostics;
using System.IO;
using Xunit;

public class GitWorktreeManagerTests
{
    [Fact]
    public void ParseWorktreeList_ParsesBranchWorktreesAndDetachedWorktrees()
    {
        const string output = """
            worktree /repos/project
            HEAD aaaaaaa
            branch refs/heads/main

            worktree /repos/project-feature
            HEAD bbbbbbb
            branch refs/heads/feature/worktrees

            worktree /repos/project-detached
            HEAD ccccccc
            detached

            """;

        var worktrees = GitWorktreeManager.ParseWorktreeList(output);

        Assert.Collection(
            worktrees,
            main =>
            {
                Assert.True(main.IsMain);
                Assert.Equal("/repos/project", main.Path);
                Assert.Equal("main", main.BranchName);
            },
            feature =>
            {
                Assert.False(feature.IsMain);
                Assert.Equal("feature/worktrees", feature.BranchName);
            },
            detached => Assert.Null(detached.BranchName));
    }

    [Fact]
    public void Remove_RequiresForceForAnUntrackedFileThenRemovesTheWorktree()
    {
        var repositoryPath = Path.Combine(Path.GetTempPath(), $"GitPruneTests-{Guid.NewGuid():N}");
        var worktreePath = Path.Combine(repositoryPath, "feature-worktree");

        try
        {
            Directory.CreateDirectory(repositoryPath);
            RunGit(repositoryPath, "init");
            RunGit(repositoryPath, "config", "user.email", "tests@example.com");
            RunGit(repositoryPath, "config", "user.name", "GitPrune Tests");
            File.WriteAllText(Path.Combine(repositoryPath, "README.md"), "test repository");
            RunGit(repositoryPath, "add", "README.md");
            RunGit(repositoryPath, "commit", "-m", "Initial commit");
            RunGit(repositoryPath, "worktree", "add", "-b", "feature/worktree-prune", worktreePath);
            File.WriteAllText(Path.Combine(worktreePath, "untracked.txt"), "requires force");

            var regularRemoval = GitWorktreeManager.Remove(repositoryPath, worktreePath, force: false);
            var forcedRemoval = GitWorktreeManager.Remove(repositoryPath, worktreePath, force: true);

            Assert.False(regularRemoval.Succeeded);
            Assert.True(forcedRemoval.Succeeded, forcedRemoval.Error);
            Assert.False(Directory.Exists(worktreePath));
        }
        finally
        {
            if (Directory.Exists(repositoryPath))
            {
                Directory.Delete(repositoryPath, recursive: true);
            }
        }
    }

    [Fact]
    public void DeleteBranch_ForceDeletesABranchThatIsNotGitMerged()
    {
        var repositoryPath = Path.Combine(Path.GetTempPath(), $"GitPruneTests-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(repositoryPath);
            RunGit(repositoryPath, "init");
            RunGit(repositoryPath, "config", "user.email", "tests@example.com");
            RunGit(repositoryPath, "config", "user.name", "GitPrune Tests");
            File.WriteAllText(Path.Combine(repositoryPath, "README.md"), "test repository");
            RunGit(repositoryPath, "add", "README.md");
            RunGit(repositoryPath, "commit", "-m", "Initial commit");
            RunGit(repositoryPath, "branch", "squash-merged-feature");

            var result = GitWorktreeManager.DeleteBranch(repositoryPath, "squash-merged-feature");

            Assert.True(result.Succeeded, result.Error);
            Assert.False(BranchExists(repositoryPath, "squash-merged-feature"));
        }
        finally
        {
            if (Directory.Exists(repositoryPath))
            {
                Directory.Delete(repositoryPath, recursive: true);
            }
        }
    }

    static void RunGit(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDirectory,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo);
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, process.StandardError.ReadToEnd());
    }

    static bool BranchExists(string workingDirectory, string branchName)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("show-ref");
        startInfo.ArgumentList.Add("--verify");
        startInfo.ArgumentList.Add($"refs/heads/{branchName}");

        using var process = Process.Start(startInfo);
        process.WaitForExit();
        return process.ExitCode == 0;
    }
}
