using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace GitPrune;

public static class GitWorktreeHelper
{
    const string _HEADS_REF_PREFIX = "refs/heads/";

    public sealed record BranchWorktree(string BranchName, string WorktreePath, bool IsBaseWorktree);

    public static IReadOnlyDictionary<string, BranchWorktree> GetBranchWorktrees(string repositoryPath)
    {
        var commonGitDirectory = NormalizePath(RunGit(repositoryPath, "rev-parse", "--path-format=absolute", "--git-common-dir"));
        var worktrees = new Dictionary<string, BranchWorktree>(StringComparer.Ordinal);

        foreach (var entry in ParseWorktrees(RunGit(repositoryPath, "worktree", "list", "--porcelain")))
        {
            if (string.IsNullOrWhiteSpace(entry.BranchReference) || !Directory.Exists(entry.WorktreePath))
            {
                continue;
            }

            var branchName = entry.BranchReference.StartsWith(_HEADS_REF_PREFIX, StringComparison.Ordinal)
                ? entry.BranchReference[_HEADS_REF_PREFIX.Length..]
                : entry.BranchReference;

            var worktreeGitDirectory = NormalizePath(RunGit(entry.WorktreePath, "rev-parse", "--path-format=absolute", "--absolute-git-dir"));
            var isBaseWorktree = string.Equals(worktreeGitDirectory, commonGitDirectory, GetPathComparison());

            worktrees[branchName] = new BranchWorktree(branchName, entry.WorktreePath, isBaseWorktree);
        }

        return worktrees;
    }

    public static void RemoveWorktree(string repositoryPath, string worktreePath, bool force)
    {
        var arguments = new List<string> { "worktree", "remove" };
        if (force)
        {
            arguments.Add("-f");
        }

        arguments.Add("--");
        arguments.Add(worktreePath);

        RunGit(repositoryPath, arguments.ToArray());
    }

    static IEnumerable<WorktreeEntry> ParseWorktrees(string output)
    {
        WorktreeEntry currentEntry = null;

        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
            {
                if (currentEntry != null)
                {
                    yield return currentEntry;
                    currentEntry = null;
                }

                continue;
            }

            if (line.StartsWith("worktree ", StringComparison.Ordinal))
            {
                if (currentEntry != null)
                {
                    yield return currentEntry;
                }

                currentEntry = new WorktreeEntry
                {
                    WorktreePath = line["worktree ".Length..],
                };

                continue;
            }

            if (currentEntry != null && line.StartsWith("branch ", StringComparison.Ordinal))
            {
                currentEntry.BranchReference = line["branch ".Length..];
            }
        }

        if (currentEntry != null)
        {
            yield return currentEntry;
        }
    }

    static string RunGit(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments.Where(a => !string.IsNullOrWhiteSpace(a)))
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start git.");
        var output = process.StandardOutput.ReadToEnd().Trim();
        var error = process.StandardError.ReadToEnd().Trim();

        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"git {string.Join(" ", arguments)} failed with exit code {process.ExitCode}: {error}");
        }

        return output;
    }

    static string NormalizePath(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    static StringComparison GetPathComparison() => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    sealed class WorktreeEntry
    {
        public string WorktreePath { get; set; }
        public string BranchReference { get; set; }
    }
}
