using System;
using System.Collections.Generic;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

public class AnalyticHelper
{
    public static class Keys {
        public const string DELETABLE_BRANCHES = "deletable_branches";
        public const string DELETE_BRANCH = "delete_branch";
        public const string UPDATE_AVAILABLE = "update_available";
        public const string UPDATE_APP = "update_app";
        public const string CHECK_VERSION = "check_version";
        public const string RESET_CONFIG = "reset_config";
        public const string USE_GITPRUNE = "use_gitprune";
    }

    private TelemetryClient _telemetryClient;
    private string _appVersion;
    private string _appReleaseRing;

    public AnalyticHelper(string appVersion, string appReleaseRing)
    {
        _appVersion = appVersion;
        _appReleaseRing = appReleaseRing;

        // Setup the analytics tracker
        var config = TelemetryConfiguration.CreateDefault();
        config.ConnectionString = Secrets.AnalyticsInstrumentationConnectionString;
        _telemetryClient = new TelemetryClient(config);
        _telemetryClient.Context.Device.OperatingSystem = Environment.OSVersion.ToString();
        _telemetryClient.Context.GlobalProperties.Add("app_version", _appVersion);
        _telemetryClient.Context.GlobalProperties.Add("app_release_ring", _appReleaseRing);
    }

    #region Analytic Events

    public void TrackDeletableBranches(int count) => _TrackEvent(Keys.DELETABLE_BRANCHES, new Dictionary<string, string> { { "count", count.ToString() } });

    public void TrackDeleteBranch() => _TrackEvent(Keys.DELETE_BRANCH);

    public void TrackUpdateAvailable(bool choseToUpdate) => _TrackEvent(Keys.UPDATE_AVAILABLE, new Dictionary<string, string> { { "chose_to_update", choseToUpdate.ToString() } });

    public void TrackCheckVersion() => _TrackEvent(Keys.CHECK_VERSION);

    public void TrackResetConfig() => _TrackEvent(Keys.RESET_CONFIG);

    public void TrackUseGitPrune() => _TrackEvent(Keys.USE_GITPRUNE);

    #endregion

    public void TrackException(Exception ex)
    {
        if (ex == null) return;

        try
        {
            _telemetryClient.TrackException(ex);
        } catch { } // If we can't send a tracked exception, no point in trying to send another one
    }

    public void Flush() 
    {
        _telemetryClient.Flush();
        // Give 200ms for the flush to complete
        System.Threading.Thread.Sleep(200);
    }

    void _TrackEvent(string key, Dictionary<string, string> properties = null)
    {
        try
        {
            var telemetry = new EventTelemetry(key);

            // Add custom properties if they exist
            if (properties != null)
            {
                foreach (var property in properties)
                {
                    telemetry.Properties.Add(property.Key, property.Value);
                }
            }

            // Send the analytics
            _telemetryClient.TrackEvent(telemetry);
        }
        catch (Exception ex) {
            // Try to send exception analytic
            _telemetryClient.TrackException(ex);
        }
    }
}