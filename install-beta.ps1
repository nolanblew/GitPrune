param(
    [string]$InstallDirectory = (Join-Path $HOME ".git-prune")
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$releaseDirectory = "git-prune-beta"
$archiveName = "gitprune-win-x64.zip"
$downloadUrl = "https://nolanblew.blob.core.windows.net/$releaseDirectory/$archiveName"
$installDirectoryPath = [System.IO.Path]::GetFullPath($InstallDirectory)
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("gitprune-install-" + [System.Guid]::NewGuid().ToString("N"))
$tempArchive = Join-Path $tempRoot $archiveName
$tempExtract = Join-Path $tempRoot "extract"
$shimPath = Join-Path $installDirectoryPath "gprune.cmd"

function Add-UserPathEntry {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PathEntry
    )

    $normalizedPathEntry = [System.IO.Path]::GetFullPath($PathEntry).TrimEnd("\")
    $existingUserPath = [Environment]::GetEnvironmentVariable("Path", "User")
    $userPathEntries = @()

    if (-not [string]::IsNullOrWhiteSpace($existingUserPath)) {
        $userPathEntries = $existingUserPath.Split(";", [System.StringSplitOptions]::RemoveEmptyEntries)
    }

    $pathExists = $false
    foreach ($entry in $userPathEntries) {
        $normalizedEntry = $entry.Trim().Trim('"').TrimEnd("\")
        if ($normalizedEntry.Equals($normalizedPathEntry, [System.StringComparison]::OrdinalIgnoreCase)) {
            $pathExists = $true
            break
        }
    }

    if (-not $pathExists) {
        $updatedEntries = @($userPathEntries + $normalizedPathEntry)
        [Environment]::SetEnvironmentVariable("Path", ($updatedEntries -join ";"), "User")
    }

    $sessionPathEntries = $env:Path.Split(";", [System.StringSplitOptions]::RemoveEmptyEntries)
    foreach ($entry in $sessionPathEntries) {
        $normalizedEntry = $entry.Trim().Trim('"').TrimEnd("\")
        if ($normalizedEntry.Equals($normalizedPathEntry, [System.StringComparison]::OrdinalIgnoreCase)) {
            return
        }
    }

    $env:Path = "$normalizedPathEntry;$env:Path"
}

Write-Host "Welcome to the Git Prune beta installer for Windows."
Write-Host "Downloading GitPrune beta from $downloadUrl"

New-Item -ItemType Directory -Path $tempRoot | Out-Null
New-Item -ItemType Directory -Path $tempExtract | Out-Null

try {
    Invoke-WebRequest -UseBasicParsing -Uri $downloadUrl -OutFile $tempArchive

    if (Test-Path -LiteralPath $installDirectoryPath) {
        Get-ChildItem -LiteralPath $installDirectoryPath -Force | Remove-Item -Recurse -Force
    }
    else {
        New-Item -ItemType Directory -Path $installDirectoryPath | Out-Null
    }

    Expand-Archive -LiteralPath $tempArchive -DestinationPath $tempExtract -Force

    Get-ChildItem -LiteralPath $tempExtract -Force | Copy-Item -Destination $installDirectoryPath -Recurse -Force

    if (-not (Test-Path -LiteralPath (Join-Path $installDirectoryPath "GitPrune.exe"))) {
        throw "GitPrune.exe was not found after extracting the archive."
    }

    @"
@echo off
"%~dp0GitPrune.exe" %*
"@ | Set-Content -LiteralPath $shimPath -Encoding ASCII

    Add-UserPathEntry -PathEntry $installDirectoryPath

    Write-Host ""
    Write-Host "Installed successfully to $installDirectoryPath"
    Write-Host "You can now run: GitPrune.exe"
    Write-Host "Or use the shortcut: gprune"
    Write-Host ""
    Write-Host "If your current terminal does not pick up the PATH change, open a new one."
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
