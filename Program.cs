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

// Authentication commands work outside repositories and never start pruning.
var loginRequested = args.Any(a => a is "--login" or "--reauth");
var logoutRequested = args.Contains("--logout");
if (loginRequested || logoutRequested)
{
    try
    {
        if (loginRequested && logoutRequested)
            throw new ArgumentException("Use --logout or --login/--reauth separately.");
        if (logoutRequested)
        {
            SettingsManager.DeleteSettingsFile();
            Console.WriteLine("Logged out locally. The saved GitHub token has been removed.");
        }
        else
        {
            await new GithubManager(new Settings()).SetCredentialsAsync(forceReLogin: true);
            Console.WriteLine("Logged in to GitHub. Credentials saved.");
        }
    }
    catch (Exception ex)
    {
        ReportGithubFailure(ex);
    }
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

// Every worktree shares this directory, so the repository configuration stays consistent.
var gitDirectory = GitWorktreeManager.GetCommonGitDirectory(workingDirectory);

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
    ReportGithubFailure(ex);
    analytics.Flush();
    return;
}

var localBranches = repo.Branches
    .Where(b => !b.IsRemote && !_BRANCHES_TO_EXCLUDE.Contains(b.FriendlyName))
    .ToArray();

var worktrees = Array.Empty<GitWorktree>();
var currentWorktreePath = string.Empty;
try
{
    worktrees = GitWorktreeManager.List(workingDirectory).ToArray();
    currentWorktreePath = GitWorktreeManager.GetCurrentWorktreePath(workingDirectory);
}
catch (Exception ex)
{
    Console.WriteLine($"Unable to inspect worktrees. Only ordinary branch pruning will be available. Error: {ex.Message}");
    analytics.TrackException(ex);
}

bool IsProtectedWorktree(GitWorktree worktree)
    => worktree.IsMain || GitWorktreeManager.PathsEqual(worktree.Path, currentWorktreePath);

var protectedBranchNames = new HashSet<string>(
    worktrees
        .Where(IsProtectedWorktree)
        .Where(w => !string.IsNullOrWhiteSpace(w.BranchName))
        .Select(w => w.BranchName),
    StringComparer.Ordinal);

// Never delete the checked-out branch, even when worktree discovery was unavailable.
if (repo.Head?.FriendlyName != null)
{
    protectedBranchNames.Add(repo.Head.FriendlyName);
}

var worktreesByBranch = worktrees
    .Where(w => !IsProtectedWorktree(w) && !string.IsNullOrWhiteSpace(w.BranchName))
    .GroupBy(w => w.BranchName, StringComparer.Ordinal)
    .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

var branchesToEvaluate = localBranches
    .Where(b => !protectedBranchNames.Contains(b.FriendlyName))
    .ToArray();
var mergedBranchNames = new HashSet<string>(StringComparer.Ordinal);

Console.WriteLine($"Found {localBranches.Length} local branches and {worktrees.Length} worktrees.");
Console.WriteLine("Determining which branches have been merged. Please wait... (this may take a minute)");

var authenticationRecovery = new AuthenticationRecovery();
try
{
for (int i = 0; i < branchesToEvaluate.Length; i += _BULK_GITHUB_REQUESTS)
{
    WriteProgress($"\rProgress: {i}/{branchesToEvaluate.Length}");

    var lowIndex = i;
    var highIndex = Math.Min(i + _BULK_GITHUB_REQUESTS, branchesToEvaluate.Length);
    var results = await authenticationRecovery.ExecuteAsync(
        () => Task.WhenAll(branchesToEvaluate[lowIndex..highIndex]
            .Select(async b => (LocalBranch: b, PullRequest: await _githubManager.FindClosedPullRequestFromBranchName(b.FriendlyName)))),
        async () =>
        {
            Console.WriteLine("\nGitHub rejected your saved credentials (401). Please sign in again in your browser.");
            await _githubManager.SetCredentialsAsync(forceReLogin: true);
            Console.WriteLine("Signed in. Retrying the interrupted batch...");
        },
        analytics.TrackException);
    foreach (var result in results.Where(r => r.PullRequest?.Merged ?? false))
    {
        mergedBranchNames.Add(result.LocalBranch.FriendlyName);
    }
}
}
catch (Exception ex)
{
    ReportGithubFailure(ex);
    analytics.Flush();
    return;
}

WriteProgress($"\rProgress: {branchesToEvaluate.Length}/{branchesToEvaluate.Length}");
Console.WriteLine();
Console.WriteLine("Done.");

var pruneTargets = localBranches
    .Where(b => !protectedBranchNames.Contains(b.FriendlyName) && mergedBranchNames.Contains(b.FriendlyName))
    .Select(b => new PrunableTarget(
        b,
        worktreesByBranch.TryGetValue(b.FriendlyName, out var worktree) ? worktree : null))
    .ToArray();

var worktreeTargetCount = pruneTargets.Count(target => target.Worktree != null);
analytics.TrackDeletableBranches(pruneTargets.Length);
analytics.TrackDeletableWorktrees(worktreeTargetCount);

if (pruneTargets.Length == 0)
{
    Console.WriteLine("You have no local branches or worktrees associated with a merged PR. Congrats!");
    analytics.Flush();
    return;
}

var worktreeTargets = pruneTargets.Where(target => target.Worktree != null).ToArray();
var branchTargets = pruneTargets.Where(target => target.Worktree == null).ToArray();

Console.WriteLine("Items associated with merged PRs:");
Console.WriteLine();
Console.WriteLine($"Worktrees ({worktreeTargets.Length})");
if (worktreeTargets.Length == 0)
{
    Console.WriteLine("  None");
}
foreach (var target in worktreeTargets)
{
    Console.WriteLine($"  • {target.Branch.FriendlyName}");
    Console.WriteLine($"    {target.Worktree.Path}");
}

Console.WriteLine();
Console.WriteLine($"Branches ({branchTargets.Length})");
if (branchTargets.Length == 0)
{
    Console.WriteLine("  None");
}
foreach (var target in branchTargets)
{
    Console.WriteLine($"  • {target.Branch.FriendlyName}");
}

if (isCommitting)
{
    Console.WriteLine();
    Console.WriteLine("Choose what to delete:");
    Console.WriteLine("\t[a] All listed branches and worktrees");
    Console.WriteLine("\t[w] Worktrees only (keep their local branches)");
    Console.WriteLine("\t[b] Branches only (skip branches checked out in linked worktrees)");
    Console.Write("a/w/b (default: cancel) >");

    var selection = ParsePruneSelection(Console.ReadLine());
    if (selection == PruneSelection.None)
    {
        Console.WriteLine("No branches or worktrees were deleted.");
    }
    else
    {
        var deletedBranches = new List<string>();
        var failedBranches = new List<(string Name, string Error)>();
        var deletedWorktrees = new List<string>();
        var failedWorktrees = new List<(string Path, string Error)>();
        var branchTargetsToDelete = new List<PrunableTarget>();
        var deletionProgress = TerminalProgress.ForConsole();

        if (selection is PruneSelection.All or PruneSelection.WorktreesOnly)
        {
            Console.WriteLine();
            for (var i = 0; i < worktreeTargets.Length; i++)
            {
                var target = worktreeTargets[i];
                if (await TryRemoveWorktree(target.Worktree, i, worktreeTargets.Length, deletionProgress, deletedWorktrees, failedWorktrees)
                    && selection == PruneSelection.All)
                {
                    branchTargetsToDelete.Add(target);
                }
            }
        }

        if (selection is PruneSelection.All or PruneSelection.BranchesOnly)
        {
            branchTargetsToDelete.AddRange(branchTargets);
        }

        if (branchTargetsToDelete.Count > 0)
        {
            Console.WriteLine();
        }
        for (var i = 0; i < branchTargetsToDelete.Count; i++)
        {
            var target = branchTargetsToDelete[i];
            try
            {
                var result = await deletionProgress.RunAsync(
                    "Deleting branches",
                    i,
                    branchTargetsToDelete.Count,
                    target.Branch.FriendlyName,
                    () => GitWorktreeManager.DeleteBranchAsync(workingDirectory, target.Branch.FriendlyName),
                    result => result.Succeeded);
                if (result.Succeeded)
                {
                    analytics.TrackDeleteBranch();
                    deletedBranches.Add(target.Branch.FriendlyName);
                }
                else
                {
                    var error = DescribeBranchDeleteFailure(target.Branch.FriendlyName, result);
                    failedBranches.Add((target.Branch.FriendlyName, error));
                    analytics.TrackException(new InvalidOperationException(error));
                }
            }
            catch (Exception ex)
            {
                var error = $"Unable to start `git branch -D -- {target.Branch.FriendlyName}`: {ex.Message}";
                failedBranches.Add((target.Branch.FriendlyName, error));
                analytics.TrackException(ex);
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Deleted {deletedBranches.Count} branch{(deletedBranches.Count == 1 ? string.Empty : "es")} and {deletedWorktrees.Count} worktree{(deletedWorktrees.Count == 1 ? string.Empty : "s")}.");

        if (deletedWorktrees.Count > 0)
        {
            Console.WriteLine("Successfully deleted worktrees:");
            foreach (var path in deletedWorktrees)
                Console.WriteLine($"\t- {path}");
        }

        if (deletedBranches.Count > 0)
        {
            Console.WriteLine("Successfully deleted branches:");
            foreach (var branchName in deletedBranches)
                Console.WriteLine($"\t- {branchName}");
        }

        if (failedWorktrees.Count > 0)
        {
            Console.WriteLine("Failed to delete worktrees:");
            foreach (var failedWorktree in failedWorktrees)
                Console.WriteLine($"\t- {failedWorktree.Path}: {failedWorktree.Error}");
        }

        if (failedBranches.Count > 0)
        {
            Console.WriteLine("Failed to delete branches:");
            foreach (var failedBranch in failedBranches)
                Console.WriteLine($"\t- {failedBranch.Name}: {failedBranch.Error}");
        }
    }
}

analytics.Flush();

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

void ReportGithubFailure(Exception ex)
{
    var status = ex is Octokit.ApiException api ? $" (HTTP {(int)api.StatusCode})" : string.Empty;
    Console.WriteLine($"\nGitHub operation failed{status}: {ex.Message}");
    Console.WriteLine("Run gprune --login to sign in again. For 403/404 errors, check repository access and organization authorization; network errors may be retried later.");
    analytics.TrackException(ex);
    Environment.ExitCode = 1;
}

PruneSelection ParsePruneSelection(string value)
{
    return value?.Trim().ToLowerInvariant() switch
    {
        "a" or "all" or "y" or "yes" => PruneSelection.All,
        "w" or "worktree" or "worktrees" => PruneSelection.WorktreesOnly,
        "b" or "branch" or "branches" => PruneSelection.BranchesOnly,
        _ => PruneSelection.None,
    };
}

async Task<bool> TryRemoveWorktree(
    GitWorktree worktree,
    int completed,
    int total,
    TerminalProgress progress,
    List<string> deletedWorktrees,
    List<(string Path, string Error)> failedWorktrees)
{
    try
    {
        var result = await progress.RunAsync(
            settings.AlwaysForceWorktreeDeletion ? "Force-removing worktrees" : "Removing worktrees",
            completed,
            total,
            worktree.Path,
            () => GitWorktreeManager.RemoveAsync(workingDirectory, worktree.Path, settings.AlwaysForceWorktreeDeletion),
            result => result.Succeeded);
        if (result.Succeeded)
        {
            analytics.TrackDeleteWorktree();
            deletedWorktrees.Add(worktree.Path);
            return true;
        }

        var error = GetGitError(result);
        if (settings.AlwaysForceWorktreeDeletion)
        {
            failedWorktrees.Add((worktree.Path, error));
            return false;
        }

        Console.WriteLine($"Unable to remove worktree '{worktree.Path}': {error}");
        Console.Write("Try again with -f? y/N >");
        if (!IsYes(Console.ReadLine()))
        {
            failedWorktrees.Add((worktree.Path, error));
            return false;
        }

        var forcedResult = await progress.RunAsync(
            "Retrying with -f",
            completed,
            total,
            worktree.Path,
            () => GitWorktreeManager.RemoveAsync(workingDirectory, worktree.Path, force: true),
            result => result.Succeeded);
        if (!forcedResult.Succeeded)
        {
            failedWorktrees.Add((worktree.Path, GetGitError(forcedResult)));
            return false;
        }

        analytics.TrackDeleteWorktree();
        deletedWorktrees.Add(worktree.Path);
        Console.Write("Forced removal succeeded. Always use -f when removing worktrees? y/N >");
        if (IsYes(Console.ReadLine()))
        {
            settings.AlwaysForceWorktreeDeletion = true;
            try
            {
                SettingsManager.SaveSettings(settings, gitDirectory);
                Console.WriteLine("Saved AlwaysForceWorktreeDeletion: true in the repository configuration.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Removed the worktree, but could not save the force-delete preference: {ex.Message}");
                analytics.TrackException(ex);
            }
        }

        return true;
    }
    catch (Exception ex)
    {
        failedWorktrees.Add((worktree.Path, ex.Message));
        analytics.TrackException(ex);
        return false;
    }
}

bool IsYes(string value)
    => value?.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) == true
        || value?.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase) == true;

string GetGitError(GitCommandResult result)
    => string.IsNullOrWhiteSpace(result.Error) ? result.Output.Trim() : result.Error.Trim();

string DescribeBranchDeleteFailure(string branchName, GitCommandResult result)
{
    var gitError = GetGitError(result);
    return $"`git branch -D -- {branchName}` exited with code {result.ExitCode}: {gitError} "
        + "The branch may still be checked out by a linked worktree; run `git worktree list` to find it.";
}

enum PruneSelection
{
    None,
    All,
    WorktreesOnly,
    BranchesOnly,
}

sealed record PrunableTarget(Branch Branch, GitWorktree Worktree);
