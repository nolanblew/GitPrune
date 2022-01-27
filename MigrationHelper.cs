using System.IO;
static class MigrationHelper {

    // For migrations to 0.2.5, we moved the settings file to the .git directory.
    // This snippet will migrate the settings file to the new location if it exists
    // in the main directory. If it also exists in the .git directory but the one
    // in the .git directory is older, replace it with the one in the main directory.
    // Then delete the one in the main directory.
    public static void MoveSettingsFile(string baseRepoDirectory)
    {
        try 
        {
            var oldPath = Path.Combine(baseRepoDirectory, SettingsManager._SETTINGS_NAME);
            var newPath = Path.Combine(baseRepoDirectory, ".git", SettingsManager._SETTINGS_NAME);

            var oldFileInfo = new FileInfo(oldPath);
            var newFileInfo = new FileInfo(newPath);

            if (oldFileInfo.Exists)
            {
                // We need to move the file. Check if there is already a settings file in the .git directory
                if (newFileInfo.Exists && newFileInfo.LastWriteTimeUtc > oldFileInfo.LastWriteTimeUtc)
                {
                    // The settings file in the .git directory is newer. Delete the one in the main directory.
                    oldFileInfo.Delete();
                }
                else
                {
                    // The settings file in the .git directory is older or doesn't exist. Move the one in the main directory to the .git directory.
                    newFileInfo.Delete();
                    oldFileInfo.MoveTo(newPath);
                }
            }
        }
        catch
        {
            // We don't care if the settings file can't be moved - we'll try again on the next migration
        }
    }
}