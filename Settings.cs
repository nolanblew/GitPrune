using System.IO;
using System;
using Newtonsoft.Json;

public static class SettingsManager
{
    const string _SETTINGS_NAME = "prune_config.json";

    static readonly JsonSerializerSettings _defaultJsonSettings = new JsonSerializerSettings
    {
        NullValueHandling = NullValueHandling.Ignore,
        MissingMemberHandling = MissingMemberHandling.Ignore,
    };
    
    public static Settings GetSettings(string baseRepoPath)
    {
        var localSettingsPath = Path.Combine(baseRepoPath, _SETTINGS_NAME);
        var globalSettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _SETTINGS_NAME);

        // Find the local settings path
        if (!File.Exists(localSettingsPath))
        {
            if (File.Exists(Path.Combine(baseRepoPath, ".github", _SETTINGS_NAME)))
                localSettingsPath = Path.Combine(baseRepoPath, ".github", _SETTINGS_NAME);
            else
                localSettingsPath = string.Empty;
        }

        // Find the global settings path
        if (!File.Exists(globalSettingsPath))
            globalSettingsPath = string.Empty;
        
        var globalSettings = string.IsNullOrEmpty(globalSettingsPath)
            ? null
            : _ReadConfigFile(globalSettingsPath);

        var localSettings = string.IsNullOrEmpty(localSettingsPath)
            ? null
            : _ReadConfigFile(localSettingsPath);

        if (globalSettings == null)
            return localSettings;
        else if (localSettings == null)
            return globalSettings;
        else
            return _GetCombinedSettings(localSettings, globalSettings);
    }

    public static (string GlobalSettingsPath, string LocalSettingsPath) CreateEmptySettings(string baseRepoPath)
    {
        var jsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Include,
            Formatting = Formatting.Indented,
        };

        var globalJson = JsonConvert.SerializeObject(new { GithubToken = "" }, jsonSettings);
        var localJson = JsonConvert.SerializeObject(new { GithubOwner = "", GithubRepo = "" }, jsonSettings);

        var localSettingsPath = Path.Combine(baseRepoPath, _SETTINGS_NAME);
        var globalSettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _SETTINGS_NAME);

        try
        {
            if (!File.Exists(globalSettingsPath))
                File.WriteAllText(globalSettingsPath, globalJson);
            else 
                globalSettingsPath = null;
        }
        catch (IOException) {}

        try
        {
            if (!File.Exists(localSettingsPath))
                File.WriteAllText(localSettingsPath, localJson);
            else
                localSettingsPath = null;
        }
        catch (IOException) {}

        return (globalSettingsPath, localSettingsPath);
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
    
    static Settings _GetCombinedSettings(Settings localSettings, Settings globalSettings) =>
        new Settings
        {
            GithubToken = localSettings.GithubToken ?? globalSettings.GithubToken,
            GithubOwner = localSettings.GithubOwner ?? globalSettings.GithubOwner,
            GithubRepo = localSettings.GithubRepo ?? globalSettings.GithubRepo
        };
}

public record Settings
{
    public string GithubToken { get; set; }
    public string GithubOwner { get; set; }
    public string GithubRepo { get; set; }
}