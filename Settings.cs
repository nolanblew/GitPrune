using System;
using System.IO;
using Newtonsoft.Json;

public static class SettingsManager
{
    internal const string _SETTINGS_NAME = "prune_config.json";
    static readonly string _OAuthTokenConfigPath = Path.Combine(PathHelper.GetAppDataDirectory(), _SETTINGS_NAME);

    static readonly JsonSerializerSettings _defaultJsonSettings = new JsonSerializerSettings
    {
        NullValueHandling = NullValueHandling.Ignore,
        MissingMemberHandling = MissingMemberHandling.Ignore,
    };

    public static string GetSettingsPath(string baseRepoPath) => Path.Combine(baseRepoPath, _SETTINGS_NAME);

    public static Settings GetSettings(string baseRepoPath)
    {
        var localSettingsPath = GetSettingsPath(baseRepoPath);

        // Find the local settings path
        if (!File.Exists(localSettingsPath))
        {
            return null;
        }

        return _ReadConfigFile(localSettingsPath);
    }

    public static void SaveSettings(Settings settings, string baseRepoPath)
    {
        var localSettingsPath = GetSettingsPath(baseRepoPath);

        var json = JsonConvert.SerializeObject(settings);
        File.WriteAllText(localSettingsPath, json);
    }

    public static string CreateEmptySettings(string baseRepoPath)
    {
        var jsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Include,
            Formatting = Formatting.Indented,
        };

        var json = JsonConvert.SerializeObject(
            new Settings
            {
                BranchesToExclude = new [] { "master", "main", "development", "dev" }
            },
            jsonSettings);

        var localSettingsPath = GetSettingsPath(baseRepoPath);

        try
        {
            if (!File.Exists(localSettingsPath))
            {
                File.WriteAllText(localSettingsPath, json);
            }
        }
        catch (IOException) {}

        return localSettingsPath;
    }

    public static TokenSettings GetOauthToken()
    {
        if (!File.Exists(_OAuthTokenConfigPath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(_OAuthTokenConfigPath);
            return JsonConvert.DeserializeObject<TokenSettings>(json, _defaultJsonSettings);
        }
        catch
        {
            return null;
        }
    }

    public static void SaveOauthToken(string authToken)
    {
        var json = JsonConvert.SerializeObject(new TokenSettings { OAuthToken = authToken });
        File.WriteAllText(_OAuthTokenConfigPath, json);
    }

    public static void DeleteSettingsFile()
    {
        if (File.Exists(_OAuthTokenConfigPath))
        {
            File.Delete(_OAuthTokenConfigPath);
        }
    }

    public static void DeleteSettingsFile(string baseRepoPath)
    {
        var localSettingsPath = GetSettingsPath(baseRepoPath);
        if (File.Exists(localSettingsPath))
        {
            File.Delete(localSettingsPath);
        }
    }

    static Settings _ReadConfigFile(string configPath)
    {
        try
        {
            var json = File.ReadAllText(configPath);
            return JsonConvert.DeserializeObject<Settings>(json, _defaultJsonSettings);
        }
        catch
        {
            return null;
        }
    }
}

public record Settings
{
    //public string GithubToken { get; set; }
    public string GithubOwner { get; set; }
    public string GithubRepo { get; set; }
    public string[] BranchesToExclude { get; set; }
}

public record TokenSettings
{
    public string OAuthToken { get; set; }
}