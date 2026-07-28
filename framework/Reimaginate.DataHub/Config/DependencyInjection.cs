using System;
using Microsoft.Extensions.DependencyInjection;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.ProcessingLockService;
using System.Net.Http;
using Microsoft.Azure.Cosmos;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.Diagnostics;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.DataAccess.Commands.CreateCosmosDocuments;
using Reimaginate.DataHub.DataAccess.Commands.DeleteCosmosDocuments;
using Reimaginate.DataHub.DataAccess.Commands.UpsertCosmosDocuments;
using Reimaginate.DataHub.DataAccess.Queries.GetCosmosDocuments;
using Reimaginate.DataHub.Requests.Internal.GetLogs;
using Reimaginate.DataHub.Requests.Internal.LogEvents;
using Reimaginate.DataHub.Requests.Internal.LogSyncEvents;
using Reimaginate.DataHub.Requests.Internal.RecordSyncEventsAgainstDataHubEntities;
using Reimaginate.DataHub.Services.DataHubEntityData;
using Reimaginate.DataHub.SharedModels.Markers;
using Reimaginate.DataServices.Cosmos;
using Reimaginate.DataServices;
using Reimaginate.DataServices.Responses;
using Reimaginate.DataHub.Services.AzureManagement;
using Reimaginate.DataHub.Services.AutoNumbers;
using Reimaginate.DataHub.Services.DynamicAssemblies;
using Reimaginate.DataHub.Services.IdService;
using Reimaginate.DataHub.Services.TimeService;
using Reimaginate.DataHub.Services.EntityConfig;
using Reimaginate.DataHub.SharedModels.Core.Models.Duplicates;
using Reimaginate.DataHub.SharedModels.Core.Models.Events;
using Reimaginate.DataHub.SharedModels.Core.Models.Jobs;
using Reimaginate.Mapper;
using Reimaginate.Mediator;
using User = Reimaginate.DataHub.SharedModels.Core.User;

namespace Reimaginate.DataHub.Config;

public static class DependencyInjection
{
    private static CosmosDataServiceOptions<T> CreateDataServiceOptions<T>(CosmosClient cosmosClient, string databaseId, string containerName, bool autoCreateContainer = false)
        where T : CosmosDocument, new()
    {
        return new CosmosDataServiceOptions<T>()
        {
            ContainerName = containerName,
            CosmosClient = cosmosClient,
            DatabaseName = databaseId,
            GetItemIdFunc = i => i.id,
            GetItemPartitionKeyFunc = i => i.pk,
            PartitionKey = nameof(PartitionedDataDocument.pk),
            SetItemIdFunc = (i, id) => i.id = id,
            SetItemPartitionKeyFunc = i => i.pk = string.Empty,
            AutoCreateIfMissing = autoCreateContainer
        };
    }

    private static void AddDataService<T>(IServiceCollection services, string databaseId, string containerName, bool autoCreateContainer = false, Action<CosmosDataServiceOptions<T>> dataServiceOptionsDelegate = null) where T : CosmosDocument, new()
    {
        services.AddSingleton(sp =>
        {
            var cosmosClient = sp.GetRequiredService<CosmosClient>();
            var options = CreateDataServiceOptions<T>(cosmosClient, databaseId, containerName, autoCreateContainer);
            dataServiceOptionsDelegate?.Invoke(options);
            return options;
        });

        services.AddSingleton<IPartitionedDataService<T>>(sp =>
        {
            return new CosmosDataService<T>(sp.GetRequiredService<CosmosDataServiceOptions<T>>());
        });
    }

    private static void RegisterGeneratedMediatorGaps(IServiceCollection services)
    {
        RegisterCosmosDocumentHandlers<ResolutionPromise>(services);
        RegisterCosmosDocumentHandlers<AutoNumberSequence>(services);
        RegisterCosmosDocumentHandlers<DataHubRole>(services);
        RegisterSyncEventHandlers<MergeFailure>(services);
        RegisterSyncEventHandlers<MergeSuccess>(services);
        RegisterSyncEventHandlers<SyncFailure>(services);
        RegisterSyncEventHandlers<SyncSuccess>(services);
        RegisterEventHandlers<Alert>(services);
        RegisterEventHandlers<CustomEvent>(services);
        RegisterEventHandlers<PatchFailure>(services);
    }

    private static void RegisterCosmosDocumentHandlers<TDocument>(IServiceCollection services)
        where TDocument : CosmosDocument, new()
    {
        services.AddTransient<IHandler<CreateCosmosDocumentsCommand<TDocument>, CreateCosmosDocumentsResponse<TDocument>>, CreateCosmosDocumentsCommandHandler<TDocument>>();
        services.AddTransient<IHandler<DeleteCosmosDocumentsCommand<TDocument>, DeleteCosmosDocumentsResponse<TDocument>>, DeleteCosmosDocumentsCommandHandler<TDocument>>();
        services.AddTransient<IHandler<GetCosmosDocumentsQuery<TDocument>, PagedResults<TDocument>>, GetCosmosDocumentsQueryHandler<TDocument>>();
        services.AddTransient<IHandler<UpsertCosmosDocumentsCommand<TDocument>, UpsertCosmosDocumentsResponse<TDocument>>, UpsertCosmosDocumentsCommandHandler<TDocument>>();
    }

    private static void RegisterSyncEventHandlers<TSyncEvent>(IServiceCollection services)
        where TSyncEvent : SyncEvent, new()
    {
        services.AddTransient<IHandler<GetLogsRequest<TSyncEvent>, GetLogsResponse>, GetLogsRequestHandler<TSyncEvent>>();
        services.AddTransient<IHandler<LogEventsRequest<TSyncEvent>, LogEventsResponse>, LogEventsRequestHandler<TSyncEvent>>();
        services.AddTransient<IHandler<LogSyncEventsRequest<TSyncEvent>, LogEventsResponse>, LogSyncEventsRequestHandler<TSyncEvent>>();
        services.AddTransient<IHandler<RecordSyncEventsAgainstDataHubEntitiesRequest<TSyncEvent>, NullResponse>, RecordSyncEventsAgainstDataHubEntitiesRequestHandler<TSyncEvent>>();
    }

    private static void RegisterEventHandlers<TEvent>(IServiceCollection services)
        where TEvent : Event, new()
    {
        services.AddTransient<IHandler<LogEventsRequest<TEvent>, LogEventsResponse>, LogEventsRequestHandler<TEvent>>();
    }

    public static IServiceCollection AddDataHub(this IServiceCollection services, Action<AddDataHubServiceOptions> options = null)
    {
        Action<DatabaseOptions> databaseOptionsDelegate = _ => { };
        Action<CosmosDbOptions> cosmosDbOptionsDelegate = _ => { };
        Action<ProcessingLockRepositoryOptions> processingLockOptionsDelegate = _ => { };
        Action<CosmosDataServiceOptions<JObject>> dataHubEntityDataServiceOptionsDelegate = _ => { };
        Action<CosmosDataServiceOptions<ChangeTrackingEntry>> changeTrackingDataServiceOptionsDelegate = _ => { };
        Action<CosmosDataServiceOptions<LogEntry>> syncFailureDataServiceOptionsDelegate = _ => { };
        Action<CosmosDataServiceOptions<MergeMarker>> mergeMarkerDataServiceOptionsDelegate = _ => { };
        Action<CosmosDataServiceOptions<SyncMarker>> syncMarkerDataServiceOptionsDelegate = _ => { };
        Action<CosmosDataServiceOptions<ResolutionPromise>> resolutionPromiseDataServiceOptionsDelegate = _ => { };
        Action<CosmosDataServiceOptions<EntityConfig>> configDataServiceOptionsDelegate = _ => { };
        Action<CosmosDataServiceOptions<AutoNumberSequence>> autoNumberSequenceDataServiceOptionsDelegate = _ => { };
        Action<CosmosDataServiceOptions<User>> userDataServiceOptionsDelegate = _ => { };
        Action<CosmosDataServiceOptions<DataHubRole>> roleDataServiceOptionsDelegate = _ => { };
        Action<CosmosDataServiceOptions<Job>> jobDataServiceOptionsDelegate = _ => { };
        Action<CosmosDataServiceOptions<Duplicate>> duplicateDataServiceOptionsDelegate = _ => { };

        var addDataHubOptions = new AddDataHubServiceOptions();

        addDataHubOptions.WithDatabase = o =>
        {
            databaseOptionsDelegate = o;
            return addDataHubOptions;
        };


        addDataHubOptions.WithProcessingLockOptions = o =>
        {
            processingLockOptionsDelegate = o;
            return addDataHubOptions;
        };

        addDataHubOptions.WithDataHubEntityDataServiceOptions = o =>
        {
            dataHubEntityDataServiceOptionsDelegate = o;
            return addDataHubOptions;
        };

        addDataHubOptions.WithChangeTrackingDataServiceOptions = o =>
        {
            changeTrackingDataServiceOptionsDelegate = o;
            return addDataHubOptions;
        };

        addDataHubOptions.WithSyncFailureDataServiceOptions = o =>
        {
            syncFailureDataServiceOptionsDelegate = o;
            return addDataHubOptions;
        };

        addDataHubOptions.WithMergeMarkerDataServiceOptions = o =>
        {
            mergeMarkerDataServiceOptionsDelegate = o;
            return addDataHubOptions;
        };

        addDataHubOptions.WithSyncMarkerDataServiceOptions = o =>
        {
            syncMarkerDataServiceOptionsDelegate = o;
            return addDataHubOptions;
        };


        addDataHubOptions.WithResolutionPromiseDataServiceOptions = o =>
        {
            resolutionPromiseDataServiceOptionsDelegate = o;
            return addDataHubOptions;
        };

        addDataHubOptions.WithConfigDataServiceOptions = o =>
        {
            configDataServiceOptionsDelegate = o;
            return addDataHubOptions;
        };

        addDataHubOptions.WithAutoNumberSequenceDataServiceOptions = o =>
        {
            autoNumberSequenceDataServiceOptionsDelegate = o;
            return addDataHubOptions;
        };

        addDataHubOptions.WithUserDataServiceOptions = o =>
        {
            userDataServiceOptionsDelegate = o;
            return addDataHubOptions;
        };

        addDataHubOptions.WithRoleDataServiceOptions = o =>
        {
            roleDataServiceOptionsDelegate = o;
            return addDataHubOptions;
        };

        addDataHubOptions.WithJobDataServiceOptions = o =>
        {
            jobDataServiceOptionsDelegate = o;
            return addDataHubOptions;
        };

        addDataHubOptions.WithDuplicateDataServiceOptions = o =>
        {
            duplicateDataServiceOptionsDelegate = o;
            return addDataHubOptions;
        };

        options?.Invoke(addDataHubOptions);

        var dataStoreOptions = addDataHubOptions.DataHubOptions.DataStoreOptions;
        var databaseOptions = new DatabaseOptions
        {
            UseCosmosDatabase = _ =>
            {
                dataStoreOptions.UseDatabase = "AzureCosmosDb";
                cosmosDbOptionsDelegate = _;
                return addDataHubOptions;
            },
            UseInMemoryDatabase = () =>
            {
                dataStoreOptions.UseDatabase = "InMemory";
                return addDataHubOptions;
            }
        };
        databaseOptionsDelegate.Invoke(databaseOptions);

        var processingLockOptions = addDataHubOptions.DataHubOptions.ProcessingLockOptions;
        var processingLockRepositoryOptions = new ProcessingLockRepositoryOptions
        {
            UseRedisRepository = _ =>
            {
                processingLockOptions.UseRepository = "Redis";
                processingLockOptionsDelegate = _;
                return addDataHubOptions;
            },
            UseInMemoryRepository = () =>
            {
                processingLockOptions.UseRepository = "InMemory";
                return addDataHubOptions;
            }
        };
        processingLockOptionsDelegate.Invoke(processingLockRepositoryOptions);

        services.AddHttpClient();

        services.AddTransient<Mediator>();
        services.AddTransient<IMediator, Mediator>();
        Mediator.RegisterHandlers(services);
        RegisterGeneratedMediatorGaps(services);
        services.AddTransient<IMapper, Mapper>();
        Mapper.RegisterMaps(services);

        services.AddSingleton<IDynamicAssembliesService, DynamicAssembliesService>();

        services.AddTransient<ITimeService, TimeService>();
        services.AddTransient<IIdService, IdService>();
        services.AddTransient<IAutoNumberService, AutoNumberService>();
        services.AddSingleton<IEntityConfigService, EntityConfigService>();
        services.AddTransient<IDataHubAuthorizationService, DataHubAuthorizationService>();
        services.AddTransient<IDataHubSeedAdminService, DataHubSeedAdminService>();
        services.AddSingleton<IAzureMonitorLogsQueryClient, AzureMonitorLogsQueryClient>();
        services.AddTransient<IDataHubTraceQueryService, DataHubTraceQueryService>();

        services.Configure<NotificationServiceOptions>(addDataHubOptions.Config.GetSection(nameof(NotificationServiceOptions)));
        services.Configure<TaskHubOptions>(addDataHubOptions.Config.GetSection(nameof(TaskHubOptions)));
        services.Configure<DataHubTraceQueryOptions>(addDataHubOptions.Config.GetSection("Observability"));

        var managementSection = addDataHubOptions.Config.GetSection("ManagementApiConfig");
        var managementGroup = managementSection["ResourceGroup"];
        var managementSub = managementSection["Subscription"];
        var managementKey = managementSection["ManagementApiKey"];
        services.AddTransient<IAzureManagementService, AzureManagementService>(cfg => new AzureManagementService(managementSub, managementGroup, managementKey));


        switch (processingLockOptions.UseRepository?.ToLower())
        {
            case "inmemory":
                services.AddProcessingLockService(_ => _.WithInMemoryRepository());
                break;


            case "redis":
                var redisClientOptions = processingLockOptions.RedisClientOptions;
                services.AddProcessingLockService(_ =>
                {
                    _.WithRedisRepository(r =>
                    {
                        r.ConnString = redisClientOptions.ConnString;
                        r.ConnectTimeout = redisClientOptions.ConnectTimeout;
                        r.Protocol = redisClientOptions.Protocol;
                        r.SyncTimeout = redisClientOptions.SyncTimeout;
                    });
                });
                break;

        }

        var cosmosDbOptions = dataStoreOptions.CosmosDbOptions;
        cosmosDbOptionsDelegate.Invoke(cosmosDbOptions);

        services.AddSingleton(serviceProvider =>
        {
            var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            var cosmosClientOptions = new CosmosClientOptions()
            {
                Serializer = new DataHubDataSerializer(),
                MaxRetryAttemptsOnRateLimitedRequests = cosmosDbOptions.MaxRetryAttemptsOnRateLimitedRequests,
                MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(cosmosDbOptions.MaxRetryWaitSecondsOnRateLimitedRequests),
                HttpClientFactory = httpClientFactory.CreateClient,
                RequestTimeout = TimeSpan.FromMinutes(5),
                ConnectionMode = cosmosDbOptions.UseGateway.GetValueOrDefault(false) ? ConnectionMode.Gateway : ConnectionMode.Direct,
            };

            return CosmosClientFactory.Create(cosmosDbOptions, cosmosClientOptions);
        });


        services.AddSingleton<IDataHubEntityDataService>(sp =>
        {
            var dataServiceOptions = new CosmosDataServiceOptions<JObject>()
            {
                DatabaseName = cosmosDbOptions.Database,
                ContainerName = cosmosDbOptions.DataContainer,
                AutoCreateIfMissing = cosmosDbOptions.AutoCreateContainers,
                CosmosClient = sp.GetRequiredService<CosmosClient>(),
                GetItemIdFunc = i => i.Value<string>(nameof(PartitionedDataDocument.id)),
                GetItemPartitionKeyFunc = i => i.Value<string>(nameof(PartitionedDataDocument.pk)),
                PartitionKey = nameof(PartitionedDataDocument.pk),
                SetItemPartitionKeyFunc = i => i[nameof(PartitionedDataDocument.pk)] = string.Empty,
                SetItemIdFunc = (i, id) => i[nameof(PartitionedDataDocument.id)] = id
            };

            dataHubEntityDataServiceOptionsDelegate?.Invoke(dataServiceOptions);
            return new DataHubEntityDataService(dataServiceOptions);
        });

        AddDataService(services, cosmosDbOptions.Database, cosmosDbOptions.TrackingDataContainer, cosmosDbOptions.AutoCreateContainers, changeTrackingDataServiceOptionsDelegate);
        AddDataService(services, cosmosDbOptions.Database, cosmosDbOptions.SyncFailuresContainer, cosmosDbOptions.AutoCreateContainers, syncFailureDataServiceOptionsDelegate);
        AddDataService(services, cosmosDbOptions.Database, cosmosDbOptions.SyncMarkersContainer, cosmosDbOptions.AutoCreateContainers, mergeMarkerDataServiceOptionsDelegate);
        AddDataService(services, cosmosDbOptions.Database, cosmosDbOptions.SyncMarkersContainer, cosmosDbOptions.AutoCreateContainers, syncMarkerDataServiceOptionsDelegate);
        AddDataService(services, cosmosDbOptions.Database, cosmosDbOptions.ResolutionPromisesContainer, cosmosDbOptions.AutoCreateContainers, resolutionPromiseDataServiceOptionsDelegate);
        AddDataService(services, cosmosDbOptions.Database, cosmosDbOptions.ConfigsContainer, cosmosDbOptions.AutoCreateContainers, configDataServiceOptionsDelegate);
        AddDataService(services, cosmosDbOptions.Database, cosmosDbOptions.ConfigsContainer, cosmosDbOptions.AutoCreateContainers, autoNumberSequenceDataServiceOptionsDelegate);
        AddDataService(services, cosmosDbOptions.Database, cosmosDbOptions.ManagementContainer, cosmosDbOptions.AutoCreateContainers, userDataServiceOptionsDelegate);
        AddDataService(services, cosmosDbOptions.Database, cosmosDbOptions.ManagementContainer, cosmosDbOptions.AutoCreateContainers, roleDataServiceOptionsDelegate);
        AddDataService(services, cosmosDbOptions.Database, cosmosDbOptions.ManagementContainer, cosmosDbOptions.AutoCreateContainers, jobDataServiceOptionsDelegate);
        AddDataService(services, cosmosDbOptions.Database, cosmosDbOptions.ManagementContainer, cosmosDbOptions.AutoCreateContainers, duplicateDataServiceOptionsDelegate);

        services.AddSingleton<IAutoNumberSequenceStore>(sp =>
        {
            var options = CreateDataServiceOptions<AutoNumberSequence>(
                sp.GetRequiredService<CosmosClient>(),
                cosmosDbOptions.Database,
                cosmosDbOptions.ConfigsContainer,
                cosmosDbOptions.AutoCreateContainers);
            autoNumberSequenceDataServiceOptionsDelegate?.Invoke(options);
            return new CosmosAutoNumberSequenceStore(options);
        });
        
        return services;
    }
}
