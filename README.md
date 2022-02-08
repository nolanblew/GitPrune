#### Build status:
![example branch parameter](https://github.com/nolanblew/GitPrune/actions/workflows/dotnet.yml/badge.svg?branch=main)

# Git Prune
This program is intended to be an alternative `prune` method to Git's [prune](https://git-scm.com/docs/git-prune) with the intent of removing local git branches that have already been merged into GitHub (currently only supports GitHub) by querying GitHub's API to look for matching PRs that have been merged.

The key advantage of this is that it will work even when `Squash and Merge` is the default merge-style to a branch - something that Git's `prune` functionality cannot handle as the HEAD is not present in the base branch.

## Usage

### Installation
**Windows**: Coming Soon

**Linux / OSX / WSL**:
Run:
```bash
curl -fsSL https://nolanblew.blob.core.windows.net/git-prune/install.sh | bash
```

Note: You must have root priveleges OR be able to run as `sudo`

Follow the instructions, or alternatively open your `~/.profile`, `~/.bashrc`, `~/.zshrc` (on Zsh) or `~/.cshrc` (on OXS) and add:
```bash
alias gprune=~/.git-prune/GitPrune
```

#### Basic usage

Ensure you're current working directory is a `git` repository and matches the repository that was compiled in `Secrets`, or pass it in to the first argument

**Windows**:
```shell
GitPrune.exe [Git Directory] [-i]
```

**Linux**
```shell
gprune [Git Directory] [-i]
```

**OSX**
```shell
gprune [Git Directory] [-i]
```

#### Arguments
 - (Optional) `[Git Directory]`: This is the directory that contains the `git` repo you want to compare against. If not provided, your current working directory will be used
 - (Optional) `[-i]`: Use this to find out which branches _would_ be deleted without having the ability to delete them. Note: You will _always_ be prompted if you want to delete the local branches if this is not used. This will just not allow you to actually delete any branches.

## Downloading
You can find the latest packaged version in the Releases section. You can also find the latest branch in the `Actions` artifacts

## Compiling
Requirements:
 - [Visual Studio Code](https://visualstudio.microsoft.com/) or [Visual Studio 2019](https://visualstudio.microsoft.com/) (VS Code was used for development, so there is no `.sln` file for Visual Studio 2019 to pick up on)
 - Windows, Mac or Linux (x64)
 - [Git](https://git-scm.com/downloads) installed on your machine
 - [.NET 5 SDK](https://dotnet.microsoft.com/download/dotnet/5.0)

Note: Currently there is only a publishing script for `.bat` (Windows). Feel free to add support for other platforms

1. Clone the repo
0. Add the `Secrets` class that implements `ISecrets` (more info below)
0. Build/Run!
0. Publish to build distributions for Windows, Mac, and Linux

## Secrets
Currently the way secrets are embedded into the app is through as `Secrets` class that implements the already-existing `ISecrets` interface.

It is recommended to create a file named `Secrets.cs` as this is already excluded in `.gitignore` and will not be uploaded

Here is the template (replace the owner and repo with your own repository):

#### Secrets.cs
```c#
public class Secrets : ISecrets
{
    public static string ClientId => "[client id for oauth]";
    public static string ClientSecret => "[client secret for oauth]";
    public static string AzureBlobTableKey => "[access key for Azure Table Blob that holds the version number]";
}
```

## Publishing
To publish, make sure you have the `dotnet` directory in your environemnt path. Then in the root directory of the repository run `publish.bat` (Widnows only) to generate the packages.

These packages are self-contained, meaning they include .NET 5 and .NET 5 Mono for the respective platform. This results in filesizes ~64MB, but ensures it will run on systems that don't have .NET or mono installed. You can also remove the `--self-contained` to remove the .NET runtime, making the executables ~2MB but requiring the runtime to be installed on the computer you are using.
