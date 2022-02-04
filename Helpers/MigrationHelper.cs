using System;
using System.IO;
static class MigrationHelper {

    // For migrations to 0.2.6, the json file in the root directory was the oauth token, incorrectly in the root directory.
    // This will delete the file, and allow the new file to be created where it should be created.
    public static void DeleteSettingsFile(string baseRepoDirectory)
    {
        try
        {
            if (File.Exists(Path.Combine(baseRepoDirectory, SettingsManager._SETTINGS_NAME)))
            {
                File.Delete(Path.Combine(baseRepoDirectory, SettingsManager._SETTINGS_NAME));
            }
        }
        catch
        {
            // Display an error message telling the user it couldn't delete the bad file
            Console.WriteLine("ERROR: Could not delete the prune_settings.json in the root directory. Please remove yourself.");
        }
    }
}