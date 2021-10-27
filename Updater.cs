using Azure.Data.Tables;
using System;
using System.Linq;

namespace GitPrune
{
    public class Updater
    {
        const string _RELEASE_RING = "stable";

        public Updater()
        {
            _tableClient = new TableServiceClient(
                new Uri("https://nolanblew.table.core.windows.net/"),
                new TableSharedKeyCredential("nolanblew", Secrets.AzureBlobTableKey))
                .GetTableClient("gitprune");
        }

        // Cloud Table Storage
        private TableClient _tableClient;


        public bool UpdateAvailable()
        {
            var result = _tableClient.Query<TableEntity>(e => e.PartitionKey == "version" && e.RowKey == _RELEASE_RING);

            var versionResult = result.FirstOrDefault();
            if (versionResult == null) { throw new Exception("Could not query update table."); }

            var strVersion = versionResult.GetString("version_number");
            var newVersion = Version.Parse(strVersion);

            // Get the current version of the app
            var currentVersion = GetType().Assembly.GetName().Version;

            return currentVersion < newVersion;
        }
    }
}