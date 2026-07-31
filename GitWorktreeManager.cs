using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

public sealed class GitWorktree
{
    public GitWorktree(string path, string branchName, bool isMain)
    {
        Path = path;
        BranchName = branchName;
        IsMain = isMain;
    }

    public string Path { get; }
    public string BranchName { get; }
    public bool IsMain { get; }
}

public sealed class GitCommandResult
{
    public GitCommandResult(int exitCode, string output, string error)
    {
        ExitCode = exitCode;
        Output = output;
        Error = error;
    }

    public int ExitCode { get; }
    public string Output { get; }
    public string Error { get; }
    public bool Succeeded => ExitCode == 0;
}

public static class GitWorktreeManager
{
    const string _BRANCH_PREFIX = "refs/heads/";

    public static IReadOnlyList<GitWorktree> List(string workingDirectory)
    {
        var result = Run(workingDirectory, "worktree", "list", "--porcelain");
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Unable to list git worktrees: {GetError(result)}");
        }

        return ParseWorktreeList(result.Output);
    }

    public static IReadOnlyList<GitWorktree> ParseWorktreeList(string output)
    {
        var worktrees = new List<GitWorktree>();
        string path = null;
        string branchName = null;

        void AddWorktree()
        {
            if (string.IsNullOrWhiteSpace(path)) return;

            worktrees.Add(new GitWorktree(path, branchName, worktrees.Count == 0));
            path = null;
            branchName = null;
        }

        foreach (var line in (output ?? string.Empty).Replace("\r\n", "\n").Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                AddWorktree();
                continue;
            }

            if (line.StartsWith("worktree ", StringComparison.Ordinal))
            {
                AddWorktree();
                path = line["worktree ".Length..];
                continue;
            }

            if (line.StartsWith("branch ", StringComparison.Ordinal))
            {
                var branch = line["branch ".Length..];
                branchName = branch.StartsWith(_BRANCH_PREFIX, StringComparison.Ordinal)
                    ? branch[_BRANCH_PREFIX.Length..]
                    : branch;
            }
        }

        AddWorktree();
        return worktrees;
    }

    public static string GetCommonGitDirectory(string workingDirectory)
    {
        var result = Run(workingDirectory, "rev-parse", "--git-common-dir");
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Unable to find the common git directory: {GetError(result)}");
        }

        return ToAbsolutePath(workingDirectory, result.Output.Trim());
    }

    public static string GetCurrentWorktreePath(string workingDirectory)
    {
        var result = Run(workingDirectory, "rev-parse", "--show-toplevel");
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Unable to find the current worktree: {GetError(result)}");
        }

        return ToAbsolutePath(workingDirectory, result.Output.Trim());
    }

    public static GitCommandResult Remove(string workingDirectory, string worktreePath, bool force)
    {
        return force
            ? Run(workingDirectory, "worktree", "remove", "-f", worktreePath)
            : Run(workingDirectory, "worktree", "remove", worktreePath);
    }

    public static GitCommandResult DeleteBranch(string workingDirectory, string branchName)
    {
        // GitHub confirmation makes a forced local delete safe even for squash-merged branches.
        return Run(workingDirectory, "branch", "-D", "--", branchName);
    }

    public static bool PathsEqual(string firstPath, string secondPath)
    {
        if (string.IsNullOrWhiteSpace(firstPath) || string.IsNullOrWhiteSpace(secondPath)) return false;

        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(firstPath)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(secondPath)),
            comparison);
    }

    static GitCommandResult Run(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Unable to start git.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return new GitCommandResult(process.ExitCode, output, error);
    }

    static string ToAbsolutePath(string workingDirectory, string path)
    {
        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(workingDirectory, path));
    }

    static string GetError(GitCommandResult result)
    {
        return string.IsNullOrWhiteSpace(result.Error) ? result.Output.Trim() : result.Error.Trim();
    }
}
