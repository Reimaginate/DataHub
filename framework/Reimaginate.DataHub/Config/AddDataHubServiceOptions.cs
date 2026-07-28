using System;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Core.Models.Duplicates;
using Reimaginate.DataHub.SharedModels.Core.Models.Jobs;
using Reimaginate.DataHub.SharedModels.Markers;
using Reimaginate.DataServices.Cosmos;

namespace Reimaginate.DataHub.Config;

public class AddDataHubServiceOptions
{
    internal IConfiguration Config { get; set; }
    internal DataHubOptions DataHubOptions { get; set; } = new();
    public AddDataHubServiceOptions WithAppSettingsConfig(IConfiguration config, string key = null)
    {
        Config = config ?? throw new ArgumentNullException(nameof(config));
        if (!string.IsNullOrEmpty(key))
        {
            Config = Config.GetSection(key);
        }

        Config.Bind(DataHubOptions);


        return this;
    }
    public Func<Action<DatabaseOptions>, AddDataHubServiceOptions> WithDatabase { get; set; }
    public Func<Action<ProcessingLockRepositoryOptions>, AddDataHubServiceOptions> WithProcessingLockOptions { get; set; }
    public Func<Action<CosmosDataServiceOptions<JObject>>, AddDataHubServiceOptions> WithDataHubEntityDataServiceOptions { get; set; }
    public Func<Action<CosmosDataServiceOptions<ChangeTrackingEntry>>, AddDataHubServiceOptions> WithChangeTrackingDataServiceOptions { get; set; }
    public Func<Action<CosmosDataServiceOptions<LogEntry>>, AddDataHubServiceOptions> WithSyncFailureDataServiceOptions { get; set; }
    public Func<Action<CosmosDataServiceOptions<MergeMarker>>, AddDataHubServiceOptions> WithMergeMarkerDataServiceOptions { get; set; }
    public Func<Action<CosmosDataServiceOptions<SyncMarker>>, AddDataHubServiceOptions> WithSyncMarkerDataServiceOptions { get; set; }
    public Func<Action<CosmosDataServiceOptions<ResolutionPromise>>, AddDataHubServiceOptions> WithResolutionPromiseDataServiceOptions { get; set; }
    public Func<Action<CosmosDataServiceOptions<EntityConfig>>, AddDataHubServiceOptions> WithConfigDataServiceOptions { get; set; }
    public Func<Action<CosmosDataServiceOptions<AutoNumberSequence>>, AddDataHubServiceOptions> WithAutoNumberSequenceDataServiceOptions { get; set; }
    public Func<Action<CosmosDataServiceOptions<User>>, AddDataHubServiceOptions> WithUserDataServiceOptions { get; set; }
    public Func<Action<CosmosDataServiceOptions<DataHubRole>>, AddDataHubServiceOptions> WithRoleDataServiceOptions { get; set; }
    public Func<Action<CosmosDataServiceOptions<Job>>, AddDataHubServiceOptions> WithJobDataServiceOptions { get; set; }
    public Func<Action<CosmosDataServiceOptions<Duplicate>>, AddDataHubServiceOptions> WithDuplicateDataServiceOptions { get; set; }
}
