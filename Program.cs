using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

bool isCommitting = !args.Contains("-i");

const int _BULK_GITHUB_REQUESTS = 5;
string[] _BRANCHES_TO_EXCLUDE = new string[] { "master", "main", "dev", "development" };
var _githubManager = new GithubManager();

int cursorRow = -1;

Console.WriteLine("Finding Branches to Prune...");

var workingDirectory = Directory.GetCurrentDirectory();
if (args.Length > 0 && Directory.Exists(args[0])) {
    workingDirectory = args[0];
}

if (!Repository.IsValid(workingDirectory)) {
    Console.WriteLine("You are not in a git directory.");
    return;
}

using var repo = new Repository(workingDirectory);

var localBranches = repo.Branches
    .Where(b => !b.IsRemote && !_BRANCHES_TO_EXCLUDE.Contains(b.FriendlyName))
    .ToArray();

var branchesToDelete = new List<Branch>();

Console.WriteLine($"Found {localBranches.Length} local branches.");
Console.WriteLine($"Determining which branches have been merged. Please wait... (this may take a minute)");

for (int i = 0; i < localBranches.Length; i += _BULK_GITHUB_REQUESTS) 
{
    WriteProgress($"\rProgress: {i}/{localBranches.Length}");

    var lowIndex = i;
    var highIndex = Math.Min(i + _BULK_GITHUB_REQUESTS, localBranches.Length);

    var tasks = localBranches[lowIndex..highIndex]
        .Select(async b => (LocalBranch: b, PullRequest: await _githubManager.FindClosedPullRequestFromBranchName(b.FriendlyName)));

    var results = await Task.WhenAll(tasks).ConfigureAwait(false);
    branchesToDelete.AddRange(results.Where(r => r.PullRequest?.Merged ?? false).Select(r => r.LocalBranch));
}

WriteProgress($"\rProgress: {localBranches.Length}/{localBranches.Length}");
Console.WriteLine();
Console.WriteLine("Done.");

if (branchesToDelete.Count == 0)
{
    Console.WriteLine("You have no local branches associated with a merged PR. Congrats!");
    return;
}

Console.WriteLine("Branches that will be deleted:");
foreach (var branch in branchesToDelete)
    Console.WriteLine($"\t- {branch.FriendlyName}");

if (isCommitting)
{
    Console.WriteLine();
    Console.WriteLine(
        branchesToDelete.Count switch {
            1 => $"Do you wish to delete this branch from your local Git?",
            _ => "Do you wish to delete these branches from your local Git?"
        }
    );

    Console.Write("y/N >");
    var result = Console.ReadLine().ToLower().Trim();
    if (result == "y" || result == "yes")
    {
        foreach(var branch in branchesToDelete)
            repo.Branches.Remove(branch);

        Console.WriteLine($"Deleted {branchesToDelete.Count()} branches.");
    }
}

void WriteProgress(string message)
{
    try
    {
        if (cursorRow == -1) cursorRow = Console.CursorTop;
        Console.SetCursorPosition(0, cursorRow);

        // Clear the current line
        Console.Write(new string(' ', Console.LargestWindowWidth));
        Console.SetCursorPosition(0, cursorRow);

        Console.Write(message);
    }
    catch
    {
        Console.WriteLine(message);
    }
}