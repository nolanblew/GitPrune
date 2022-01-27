using System.Text;
using System.Diagnostics;
using System.IO;
using Azure.Data.Tables;
using System;
using System.Linq;
using ICSharpCode.SharpZipLib.Zip;
using ICSharpCode.SharpZipLib.Tar;
using ICSharpCode.SharpZipLib.GZip;

namespace GitPrune
{
    public class Updater
    {
        const string _RELEASE_RING =

#if BETA
            "stable";
#else
            "beta";
#endif

        public Updater()
        {
            _tableClient = new TableServiceClient(
                new Uri("https://nolanblew.table.core.windows.net/"),
                new TableSharedKeyCredential("nolanblew", Secrets.AzureBlobTableKey))
                .GetTableClient("gitprune");
        }

        public Version AppVersion => GetType().Assembly.GetName().Version;

        // Cloud Table Storage
        private TableClient _tableClient;

        private string _GitPruneFileName { get; } = Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName);
        private string _GitPruneDirectory { get; } = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);

        public void CompleteUpdateIfNeeded()
        {
            var currentDirectory = new DirectoryInfo(_GitPruneDirectory);
            
            if (currentDirectory.Name != "update")
            {
                // If there is a directory called "update", delete it
                var updateDirectory = currentDirectory.GetDirectories("update").FirstOrDefault();
                if (updateDirectory != null)
                {
                    // Wait a second for the previous update process to finish
                    System.Threading.Thread.Sleep(1000);

                    updateDirectory.Delete(true);
                }

                return;
            }

            Console.WriteLine("Completing update...");

            // Wait a second for the previous update process to finish
            System.Threading.Thread.Sleep(1000);

            // Delete everything from the parent directory except the update folder
            var parentDirectory = currentDirectory.Parent;
            try
            {
                foreach (var file in parentDirectory.GetFiles())
                {
                    file.Delete();
                }
                foreach (var directory in parentDirectory.GetDirectories())
                {
                    if (directory.Name == "update") { continue; }
                    directory.Delete(true);
                }

                // Now try to copy the contents of the update folder to the parent directory
                foreach (var file in currentDirectory.GetFiles())
                {
                    file.CopyTo(Path.Combine(parentDirectory.FullName, file.Name));
                }
                foreach (var directory in currentDirectory.GetDirectories())
                {
                    CopyDirectory(directory, new DirectoryInfo(Path.Combine(parentDirectory.FullName, directory.Name)));
                }

                // Finally, run the GitPrune process in the parent directory
                var gitPruneProcess = new System.Diagnostics.Process();
                var gitPruneFileInfo = new FileInfo(Path.Combine(parentDirectory.FullName, _GitPruneFileName));

                if (!gitPruneFileInfo.Exists)
                {
                    throw new FileNotFoundException("GitPrune not found in folder.");
                }

                // The filename should be the same as the current executable, but in the parent diretory
                gitPruneProcess.StartInfo.FileName = gitPruneFileInfo.FullName;
                gitPruneProcess.StartInfo.WorkingDirectory = parentDirectory.FullName;
                gitPruneProcess.StartInfo.UseShellExecute = false;

                gitPruneProcess.Start();
                Environment.Exit(0);

            } catch (Exception e)
            {
                Console.WriteLine("Failed to complete update. Please delete the GitPrune folder and try again, or re-run the install script." + e.Message);
                Environment.Exit(-1);
            }

        }

        public bool UpdateAvailable()
        {
            var result = _tableClient.Query<TableEntity>(e => e.PartitionKey == "version" && e.RowKey == _RELEASE_RING);

            var versionResult = result.FirstOrDefault();
            if (versionResult == null) { throw new Exception("Could not query update table."); }

            var strVersion = versionResult.GetString("version_number");
            var newVersion = Version.Parse(strVersion);

            return AppVersion < newVersion;
        }

        public void Update() {
            // Download the zip file to a temp file
            var tempFile = Path.GetTempFileName();

            // Download the zip file based on the platform
            var zipUrl = "";
            if (System.OperatingSystem.IsWindows()) {
                zipUrl = "https://nolanblew.blob.core.windows.net/git-prune/gitprune-win-x64.zip";
                tempFile += ".zip";
            } else if (System.OperatingSystem.IsMacOS()) {
                zipUrl = "https://nolanblew.blob.core.windows.net/git-prune/gitprune-osx-x64.tar.gz";
                tempFile += ".tar.gz";
            } else if (System.OperatingSystem.IsLinux()) {
                zipUrl = "https://nolanblew.blob.core.windows.net/git-prune/gitprune-linux-x64.tar.gz";
                tempFile += ".tar.gz";
            } else {
                throw new Exception("Unsupported platform.");
            }

            Console.WriteLine("Downloading Update...");
            using (var client = new System.Net.WebClient())
            {
                client.DownloadFile(zipUrl, tempFile);
            }
            
            // Unzip the file to a folder called "update" in the current directory
            Console.WriteLine("Unzipping Update...");
            var updateDirectory = new DirectoryInfo(Path.Combine(_GitPruneDirectory, "update"));
            if (updateDirectory.Exists)
            {
                updateDirectory.Delete(true);
            }
            updateDirectory.Create();

            // Extract to the update folder using SharpZipLib
            Unzip(tempFile, updateDirectory.FullName);

            // Delete the tmp file
            File.Delete(tempFile);

            // Start the GitPrune in the update folder and close this
            var gitPruneProcess = new System.Diagnostics.Process();
            var gitPruneFileInfo = new FileInfo(Path.Combine(updateDirectory.FullName, _GitPruneFileName));

            if (!gitPruneFileInfo.Exists)
            {
                throw new FileNotFoundException("GitPrune not found in folder. Looking in: " + gitPruneFileInfo.FullName);
            }

            // Run the GitPrune in the update folder
            gitPruneProcess.StartInfo.FileName = gitPruneFileInfo.FullName;
            gitPruneProcess.StartInfo.WorkingDirectory = updateDirectory.FullName;
            gitPruneProcess.StartInfo.UseShellExecute = false;

            gitPruneProcess.Start();
            Environment.Exit(0);
        }

        void CopyDirectory(DirectoryInfo source, DirectoryInfo target)
        {
            if (!target.Exists)
            {
                target.Create();
            }

            foreach (var file in source.GetFiles())
            {
                file.CopyTo(Path.Combine(target.FullName, file.Name));
            }
            foreach (var directory in source.GetDirectories())
            {
                // Copy the directory recursively
                CopyDirectory(directory, new DirectoryInfo(Path.Combine(target.FullName, directory.Name)));
            }
        }

        void Unzip(string zipFile, string targetDirectory)
        {
            var zipFileInfo = new FileInfo(zipFile);

            if (!zipFileInfo.Exists)
            {
                throw new FileNotFoundException("Zip file not found.");
            }

            if (!Directory.Exists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            if (zipFileInfo.FullName.ToLower().EndsWith(".zip"))
            {
                var fastZip = new FastZip();
                fastZip.ExtractZip(zipFileInfo.FullName, targetDirectory, null);
            }
            else if (zipFileInfo.FullName.ToLower().EndsWith(".tar.gz"))
            {
                using var inStream = File.OpenRead(zipFileInfo.FullName);
                using var gzipStream = new GZipInputStream(inStream);

                using var tarArchive = TarArchive.CreateInputTarArchive(gzipStream, Encoding.Default);
                tarArchive.ExtractContents(targetDirectory);
                tarArchive.Close();

                gzipStream.Close();
                inStream.Close();

                // Set permissions of files to executable
                SetPermissions(targetDirectory, FilePermissions.Execute, true);
            }
            else
            {
                throw new Exception("Unsupported file type.");
            }
        }

        void SetPermissions(string item, FilePermissions permissions, bool recursive = false) {
            if (System.OperatingSystem.IsWindows()) {
                // Permissions should already be set on Windows
                return;
            }
            else
            {
                // Build permission string
                var permissionString = ((int)permissions).ToString();
                using var process = new Process {
                    StartInfo = new ProcessStartInfo {
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        FileName = "/bin/bash",
                        Arguments = $"-c \"chmod {permissionString} {item} {(recursive ? " -R" : "")}\"",
                    }
                };
                process.Start();
                process.WaitForExit();
            }
        }

        enum FilePermissions {
            Nona = 000,
            ReadOnly = 444,
            ReadWrite = 666,
            Execute = 755,
        }
    }
}