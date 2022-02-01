using System;
using System.IO;

static class PathHelper
{
    public static string GetAppDataDirectory()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrEmpty(appDataPath))
        {
            appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), ".config");
        }

        if  (!Directory.Exists(appDataPath))
        {
            Directory.CreateDirectory(appDataPath);
        }

        return appDataPath;
    }
}