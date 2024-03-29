﻿using GitPrune;
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
var analytics = new AnalyticHelper(updater.AppVersion.ToString(), Updater.RELEASE_RING);

// Fropm this point on, catch all unhandled exceptions and log them
AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
{
    try
    {
        analytics.TrackException(e.ExceptionObject as Exception);
    } catch { }

    Console.WriteLine(e.ExceptionObject);
};

analytics.TrackUseGitPrune();
Console.CancelKeyPress += (sender, e) => {
    e.Cancel = true;
    analytics.Flush();
    Environment.Exit(0);
};

// Get specific args
// -v or --version
if (args.Contains("-v") || args.Contains("--version"))
{
#if BETA
    Console.WriteLine("BETA VERSION");
#endif

    Console.WriteLine("GitPrune v" + updater.AppVersion.ToString());
    analytics.TrackCheckVersion();
    analytics.Flush();
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
            analytics.TrackResetConfig();
        }
        catch (Exception e)
        {
            Console.WriteLine("Error deleting config file: " + e.Message);
            analytics.TrackException(e);
            analytics.Flush();
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
            Console.WriteLine();
            Console.WriteLine("Updating... Please wait");
            analytics.TrackUpdateAvailable(true);
            await updater.Update();
        }
        else
        {
            analytics.TrackUpdateAvailable(false);
        }
    }
}
catch (Exception ex) {
    var errorTrace = 
#if BETA
        ex.ToString();
#else
        string.Empty;
#endif

    Console.WriteLine("Error while checking for or completing the update. Please make sure you are online next time. " + errorTrace);
    analytics.TrackException(ex);
    analytics.Flush();
}

int cursorRow = -1;

var workingDirectory = Directory.GetCurrentDirectory();
if (args.Length > 0 && Directory.Exists(args[0])) {
    workingDirectory = args[0];
}

Console.WriteLine("Finding Branches to Prune...");

if (!Repository.IsValid(workingDirectory)) {
    Console.WriteLine("You are not in a git directory.");
    analytics.Flush();
    return;
}

using var repo = new Repository(workingDirectory);

// TODO: Remove after a few versions:
// Migrate the settings file if it exists in the main directory
MigrationHelper.DeleteSettingsFile(workingDirectory);

// The gitDirectory is the directory where the .git folder is located for the settings
var gitDirectory = repo.Info.Path;
if (!gitDirectory.EndsWith(".git") &&  Directory.Exists(Path.Combine(gitDirectory, ".git"))) {
    gitDirectory = Path.Combine(gitDirectory, ".git");
}

// Check the config
var repoRemote = repo.Network.Remotes.FirstOrDefault();
var settings = SettingsManager.GetSettings(gitDirectory);


if (settings == null
    || string.IsNullOrEmpty(settings.GithubOwner)
    || string.IsNullOrEmpty(settings.GithubRepo))
{
    try
    {
        if (settings == null) { settings = new(); }

        settings = GithubManager.SetOwnerRepo(settings, repoRemote.Url, gitDirectory);
        Console.WriteLine($"Set repo owner to {settings.GithubOwner} and repo to {settings.GithubRepo}.");
        Console.WriteLine($"If this is not correct, edit the config at: {SettingsManager.GetSettingsPath(gitDirectory)}");
        Console.WriteLine();
    }
    catch
    {
        Console.WriteLine("You have not intialized any settings file or the settings file is corrupt.");
        Console.WriteLine("Attempting to create settings file...");

        var settingsPath = SettingsManager.CreateEmptySettings(gitDirectory);

        if (settingsPath != null)
            Console.WriteLine($"Local settings file created: {settingsPath}");

        Console.WriteLine("Please open your settings file and fill out the settings before running git prune.");
        analytics.Flush();
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
    analytics.Flush();
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

analytics.TrackDeletableBranches(branchesToDelete.Count);

if (branchesToDelete.Count == 0)
{
    Console.WriteLine("You have no local branches associated with a merged PR. Congrats!");
    analytics.Flush();
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
        // Check if we are currently in one of the branches to delete
        if (branchesToDelete.Any(b => b.FriendlyName == repo.Head.FriendlyName))
        {
            Console.WriteLine("You are currently on one of the branches to delete. Please switch to another branch and run Git Prune again.");
            analytics.Flush();
            return;
        }
        
        try
        {
            foreach(var branch in branchesToDelete)
            {
                repo.Branches.Remove(branch);
                analytics.TrackDeleteBranch();
            }

            Console.WriteLine($"Deleted {branchesToDelete.Count()} branches.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occured while deleting branches: {ex.Message}");
            analytics.TrackException(ex);
        }
    }

    analytics.Flush();
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