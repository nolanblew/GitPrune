using System;
using System.IO;

public static class CommandLine
{
    public const string Help = """
        GitPrune — remove local branches and linked worktrees with merged GitHub PRs

        Usage: gprune [repository-directory] [options]

        Options:
          -h, --help      Show this help and exit (no network access).
          -v, --version   Show the installed version and exit.
          -i              Preview eligible items; never delete branches or worktrees.
          --login         Sign in through your browser and save GitHub credentials.
          --reauth        Alias for --login; replace saved credentials.
          --logout        Remove saved credentials locally and exit.
          -r, --reset     Confirm clearing saved credentials, then continue pruning.

        Without -i, a prompt lets you choose:
          a  Remove listed worktrees and branches.
          w  Remove worktrees only, keeping their branches.
          b  Remove branches only, skipping branches checked out in linked worktrees.
          Enter or any other response cancels deletion.

        The base and current worktrees and their branches are protected.
        Failed deletes do not stop the remaining items. GitPrune can offer a retry
        with -f, which can discard uncommitted work. After a successful retry you
        can save AlwaysForceWorktreeDeletion in the shared git directory's
        prune_config.json. BranchesToExclude adds protected branch names there.

        Authentication commands work outside repositories and exit without pruning.
        Logout removes the local token; it does not revoke access on GitHub.
        A rejected token (HTTP 401) triggers one browser sign-in and scan retry.

        Examples:
          gprune
          gprune /path/to/repository -i
          gprune --login
          gprune --version
        """;

    public static string GetRepositoryDirectory(string[] args)
    {
        string directory = null;
        foreach (var arg in args)
        {
            if (arg is "-i" or "-r" or "--reset" or "-v" or "--version"
                or "--login" or "--reauth" or "--logout") continue;
            if (arg.StartsWith("-", StringComparison.Ordinal))
                throw new ArgumentException($"Unknown option: {arg}. Run gprune --help for usage.");
            if (directory != null)
                throw new ArgumentException("Provide only one repository directory. Run gprune --help for usage.");
            if (!Directory.Exists(arg))
                throw new ArgumentException($"Repository directory does not exist: {arg}");
            directory = Path.GetFullPath(arg);
        }
        return directory ?? Directory.GetCurrentDirectory();
    }
}
