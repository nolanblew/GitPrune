using GitPrune;
using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

bool isCommitting = !args.Contains("-i");

const int _BULK_GITHUB_REQUESTS = 5;
string[] _BRANCHES_TO_EXCLUDE = new string[] { "master", "main", "dev", "development" };

var updater = new Updater();

// Get specific args
// -v or --version
if (args.Contains("-v") || args.Contains("--version"))
{
    Console.WriteLine("GitPrune v" + updater.AppVersion.ToString());
    return;
}

// -r or --reset to reset the global config (delete it)
if (args.Contains("-r") || args.Contains("--reset"))
{
    // Ask the user if they wish to reset the config
    Console.WriteLine("Are you sure you want to clear the global config? You'll have to login again. (y/N)");
    Console.Write(">> ");
    var input = Console.ReadLine();
    if (input.ToLower() == "y")
    {
        // Delete the config file
        try {
            SettingsManager.DeleteSettingsFile();
            Console.WriteLine("Config file deleted successfully..");
            Console.WriteLine();
        }
        catch (Exception e)
        {
            Console.WriteLine("Error deleting config file: " + e.Message);
            return;
        }
    }
}

// First check for updates
try
{
    updater.CompleteUpdateIfNeeded();

    if (updater.UpdateAvailable())
    {

        Console.WriteLine("Update available. Would you like to update?");
        Console.Write("Y/n >");
        if (Console.ReadLine().ToLower().Trim() != "n")
        {
            if (!System.OperatingSystem.IsWindows() && !RootChecker.IsRoot())
            {
                Console.WriteLine("You must be root to update. Please run as sudo or root.");
                Console.WriteLine("Note: You cannot use 'sudo gprune' as gprune is an alias. Ensure to use 'sudo GitPrune'");
                return;
            }

            Console.WriteLine();
            Console.WriteLine("Updating... Please wait");
            updater.Update();
        }
    }
}
catch (Exception ex) {
    Console.WriteLine("Error while checking for or completing the update! " + ex.ToString());
    return;
}

int cursorRow = -1;

var workingDirectory = Directory.GetCurrentDirectory();
if (args.Length > 0 && Directory.Exists(args[0])) {
    workingDirectory = args[0];
}

Console.WriteLine("Finding Branches to Prune...");

if (!Repository.IsValid(workingDirectory)) {
    Console.WriteLine("You are not in a git directory.");
    return;
}

using var repo = new Repository(workingDirectory);

// Check the config
var repoRemote = repo.Network.Remotes.FirstOrDefault();
var settings = SettingsManager.GetSettings(repo.Info.Path);


if (settings == null
    || string.IsNullOrEmpty(settings.GithubOwner)
    || string.IsNullOrEmpty(settings.GithubRepo))
{
    try
    {
        if (settings == null) { settings = new(); }

        settings = GithubManager.SetOwnerRepo(settings, repoRemote.Url, repo.Info.Path);
        Console.WriteLine($"Set repo owner to {settings.GithubOwner} and repo to {settings.GithubRepo}.");
        Console.WriteLine($"If this is not correct, edit the config at: {SettingsManager.GetSettingsPath(repo.Info.Path)}");
        Console.WriteLine();
    }
    catch
    {
        Console.WriteLine("You have not intialized any settings file or the settings file is corrupt.");
        Console.WriteLine("Attempting to create settings file...");

        var settingsPath = SettingsManager.CreateEmptySettings(repo.Info.Path);

        if (settingsPath != null)
            Console.WriteLine($"Local settings file created: {settingsPath}");

        Console.WriteLine("Please open your settings file and fill out the settings before running git prune.");
        return;
    }
}

var _githubManager = new GithubManager(settings);

try
{
    await _githubManager.SetCredentialsAsync();
}
catch (Exception ex)
{
    Console.WriteLine("Unfortunately an error occured. Please try again.");
    Console.WriteLine($"Error: {ex.Message}");
    return;
}

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