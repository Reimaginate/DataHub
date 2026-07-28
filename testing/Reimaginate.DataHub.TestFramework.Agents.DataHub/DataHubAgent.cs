using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.Test.Framework;
using Reimaginate.Test.Framework.Helpers;

namespace Reimaginate.DataHub.TestFramework.Agents.DataHub;

public class DataHubAgent : TestAgentBase<DataHubAgent>
{
    public DataHubAgent()
    {

    }

    public DataHubAgent(Func<ServiceCollection> serviceCollectionBuilder) : base(serviceCollectionBuilder, DiagnosticConfig.DataHub.ActivitySource)
    {

    }


    #region DeleteDataHubEntitiesRequest

    private Func<IServiceProvider, string, Func<object, Dictionary<string, object?>, List<string>>, Func<object, Dictionary<string, object?>, Task<ScenarioActionResult>>> _deleteDataHubEntitiesRequestAction = (services, entityType, entityIdsFunc) =>
    {
        return async (currentObject, stash) =>
        {
            var entityIds = entityIdsFunc(currentObject, stash);
            if (entityIds.Any())
            {
                var dataHubClient = services.GetRequiredService<IDataHubClient>();
                await dataHubClient.PostRequestAsync<DeleteDataHubEntitiesRequest, DeleteDataHubEntitiesResponse>(new DeleteDataHubEntitiesRequest()
                {
                    EntityType = entityType,
                    EntityIds = entityIds,
                    IncludeTrackingEntries = true
                }, CancellationToken.None);
            }

            return new ScenarioActionResult() { CurrentObject = currentObject, Outputs = stash };
        };
    };

    public DataHubAgent DeleteDataHubEntities(string entityType, Func<object, Dictionary<string, object?>, List<string>> entityIdsFunc)
    {
        var action = _deleteDataHubEntitiesRequestAction(AgentServices, entityType, entityIdsFunc);
        ScenarioBuilder.Enqueue(action);
        return this;
    }

    public DataHubAgent DeleteDataHubEntity(string entityType, string entityId)
    {
        List<string> EntityIdsFunc(object currentObject, Dictionary<string, object?> stash) => [entityId];
        var action = _deleteDataHubEntitiesRequestAction(AgentServices, entityType, EntityIdsFunc);
        ScenarioBuilder.Enqueue(action);
        return this;
    }

    public DataHubAgent DeleteDataHubEntity(string entityType, Func<object, Dictionary<string, object?>, string> entityIdFunc)
    {
        List<string> EntityIdsFunc(object currentObject, Dictionary<string, object?> stash) => new() { entityIdFunc(currentObject, stash) };
        var action = _deleteDataHubEntitiesRequestAction(AgentServices, entityType, EntityIdsFunc);
        ScenarioBuilder.Enqueue(action);
        return this;
    }


    public DataHubAgent DeleteDataHubEntities(string entityType, string fromStash)
    {
        List<string> EntityIdsFunc(object currentObject, Dictionary<string, object?> stash)
        {
            var stashedEntities = stash[fromStash]!.ToObject<List<JObject>>() ?? [];
            var stashedEntityIds = stashedEntities
                .Select(s => s.Value<string>(nameof(DataHubEntity.id)))
                .OfType<string>()
                .ToList();
            return stashedEntityIds;
        }

        var action = _deleteDataHubEntitiesRequestAction(AgentServices, entityType, EntityIdsFunc);
        ScenarioBuilder.Enqueue(action);
        return this;
    }

    #endregion

    #region DeleteSourceEntities

    private readonly Func<IServiceProvider, string, string, Func<object, Dictionary<string, object?>, List<string>>, Func<object, Dictionary<string, object?>, Task<ScenarioActionResult>>> _deleteSourceEntitiesRequestAction = (services, dataSource, entityType, entityIdsFunc) =>
    {
        return async (currentObject, stash) =>
        {
            var dataHubClient = services.GetRequiredService<IDataHubClient>();
            var entityIds = entityIdsFunc(currentObject, stash);

            if (entityIds.Any())
            {

                await dataHubClient.PostRequestAsync<DeleteSourceEntitiesRequest, DeleteSourceEntitiesResponse>(new DeleteSourceEntitiesRequest()
                {
                    DataSource = dataSource,
                    EntityType = entityType,
                    EntityIds = entityIds
                }, CancellationToken.None);
            }

            return new ScenarioActionResult() { CurrentObject = currentObject, Outputs = stash };
        };
    };

    public DataHubAgent DeleteSourceEntities(string dataSource, string entityType, Func<object, Dictionary<string, object?>, List<string>> entityIdsFunc)
    {
        var action = _deleteSourceEntitiesRequestAction(AgentServices, dataSource, entityType, entityIdsFunc);
        ScenarioBuilder.Enqueue(action);
        return this;
    }

    public DataHubAgent DeleteSourceEntity(string dataSource, string entityType, string entityId)
    {
        List<string> EntityIdsFunc(object currentObject, Dictionary<string, object?> stash) => new() { entityId };
        var action = _deleteSourceEntitiesRequestAction(AgentServices, dataSource, entityType, EntityIdsFunc);
        ScenarioBuilder.Enqueue(action);
        return this;
    }

    public DataHubAgent DeleteSourceEntity(string dataSource, string entityType, Func<object, Dictionary<string, object?>, string> entityIdFunc)
    {
        List<string> EntityIdsFunc(object currentObject, Dictionary<string, object?> stash) => new() { entityIdFunc(currentObject, stash) };
        var action = _deleteSourceEntitiesRequestAction(AgentServices, dataSource, entityType, EntityIdsFunc);
        ScenarioBuilder.Enqueue(action);
        return this;
    }

    #endregion

    #region GetDataHubEntitiesById

    private Func<IServiceProvider, string, Func<object, Dictionary<string, object?>, List<string>>, string?, Func<object, Dictionary<string, object?>, Task<ScenarioActionResult>>> _getDataHubEntitiesByIdAction = (services, entityType, entityIdsFunc, stashTo) =>
    {
        return async (currentObject, stash) =>
        {
            var dataHubClient = services.GetRequiredService<IDataHubClient>();
            var getEntitiesResponse = await dataHubClient.PostRequestAsync<GetDataHubEntitiesByIdRequest, GetDataHubEntitiesByIdResponse>(new GetDataHubEntitiesByIdRequest()
            {
                EntityType = entityType,
                EntityIds = entityIdsFunc(currentObject, stash)
            }, CancellationToken.None);

            if (!string.IsNullOrEmpty(stashTo)) stash[stashTo] = getEntitiesResponse.Results.FirstOrDefault();

            return new ScenarioActionResult() { CurrentObject = getEntitiesResponse, Outputs = stash };
        };
    };

    public DataHubAgent GetDataHubEntitiesById(string entityType, Func<object, Dictionary<string, object?>, List<string>> entityIdsFunc, string? stashTo = null)
    {
        var action = _getDataHubEntitiesByIdAction(AgentServices, entityType, entityIdsFunc, stashTo);
        ScenarioBuilder.Enqueue(action);
        return this;
    }

    public DataHubAgent GetDataHubEntityById(string entityType, Func<object, Dictionary<string, object?>, string> entityIdFunc, string? stashTo = null)
    {
        List<string> EntityIdsFunc(object currentObject, Dictionary<string, object?> stash) => new() { entityIdFunc(currentObject, stash) };
        var action = _getDataHubEntitiesByIdAction(AgentServices, entityType, EntityIdsFunc, stashTo);
        ScenarioBuilder.Enqueue(action);
        return this;
    }


    #endregion

    #region GetDataHubEntitiesWhere

    private readonly Func<IServiceProvider, string, Func<object, Dictionary<string, object?>, string>, string?, Func<object, Dictionary<string, object?>, Task<ScenarioActionResult>>> _getDataHubEntitiesWhereAction = (services, entityType, whereFunc, stashTo) =>
    {
        return async (currentObject, stash) =>
        {
            var dataHubClient = services.GetRequiredService<IDataHubClient>();
            var where = whereFunc(currentObject, stash);

            var results = new List<JObject>();

            var request = new GetDataHubEntitiesWhereRequest()
            {
                PageSize = 5000,
                WhereClause = $"x.entityType = @entityType and ({where})",
                Parameters = [new DataHubQueryParameter { Name = "entityType", Value = entityType }]
            };
            var getEntitiesResponse = await dataHubClient.PostRequestAsync<GetDataHubEntitiesWhereRequest, GetDataHubEntitiesResponse>(request, CancellationToken.None);
            results.AddRange(getEntitiesResponse.Results);


            while (getEntitiesResponse.MoreResultsAvailable)
            {
                request.ContinuationToken = getEntitiesResponse.ContinuationToken;
                getEntitiesResponse = await dataHubClient.PostRequestAsync<GetDataHubEntitiesWhereRequest, GetDataHubEntitiesResponse>(request, CancellationToken.None);

                results.AddRange(getEntitiesResponse.Results);
            }

            if (!string.IsNullOrEmpty(stashTo)) stash[stashTo] = results;

            return new ScenarioActionResult() { CurrentObject = results, Outputs = stash };
        };
    };

    public DataHubAgent GetDataHubEntitiesWhere(string entityType, Func<object, Dictionary<string, object?>, string> whereFunc, string? stashTo = null)
    {
        var action = _getDataHubEntitiesWhereAction(AgentServices, entityType, whereFunc, stashTo);
        ScenarioBuilder.Enqueue(action);
        return this;
    }

    public DataHubAgent GetDataHubEntitiesWhere(string entityType, string where, string? stashTo = null)
    {
        string WhereFunc(object currentObject, Dictionary<string, object?> stash) => where;
        var action = _getDataHubEntitiesWhereAction(AgentServices, entityType, WhereFunc, stashTo);
        ScenarioBuilder.Enqueue(action);
        return this;
    }


    #endregion

    #region UpdateDataHubEntity

    private readonly Func<IServiceProvider, Func<object, Dictionary<string, object?>, UpdateEntityRequest>, string?, Func<object, Dictionary<string, object?>, Task<ScenarioActionResult>>> _updateDataHubEntityAction = (services, requestFunc, stashTo) =>
    {
        return async (currentObject, stash) =>
        {
            var dataHubClient = services.GetRequiredService<IDataHubClient>();
            var request = requestFunc(currentObject, stash);
            var updateDataHubEntityResponse = await dataHubClient.PostRequestAsync<UpdateEntityRequest, UpdateEntityResponse>(request, CancellationToken.None);

            if (!string.IsNullOrEmpty(stashTo))
            {
                stash[stashTo] = updateDataHubEntityResponse.ResultingEntity;
                stash[$"{stashTo}_updateResponse"] = updateDataHubEntityResponse;
            }

            return new ScenarioActionResult() { CurrentObject = updateDataHubEntityResponse, Outputs = stash };
        };
    };

    public DataHubAgent UpdateDataHubEntity(Func<object, Dictionary<string, object?>, UpdateEntityRequest> requestFunc, string? stashTo = null)
    {
        var action = _updateDataHubEntityAction(AgentServices, requestFunc, stashTo);
        ScenarioBuilder.Enqueue(action);
        return this;
    }

    public DataHubAgent UpdateDataHubEntity<TDataHubEntity>(Func<object, Dictionary<string, object?>, TDataHubEntity> entityFunc, string? stashTo = null) where TDataHubEntity : DataHubEntity
    {
        UpdateEntityRequest UpdateFunc(object currentObject, Dictionary<string, object?> stash)
        {
            var updatedEntity = entityFunc(currentObject, stash);
            return new UpdateEntityRequest()
            {
                DataSource = DataSources.DataHub,
                EntityType = typeof(TDataHubEntity).Name,
                EntityId = updatedEntity.id,
                Data = JObject.FromObject(updatedEntity)
            };
        }

        var action = _updateDataHubEntityAction(AgentServices, UpdateFunc, stashTo);
        ScenarioBuilder.Enqueue(action);
        return this;
    }

    public DataHubAgent UpdateDataHubEntity<TDataHubEntity>(TDataHubEntity updatedEntity, string stashTo) where TDataHubEntity : DataHubEntity
    {
        UpdateEntityRequest UpdateFunc(object currentObject, Dictionary<string, object?> stash)
        {
            return new UpdateEntityRequest()
            {
                DataSource = DataSources.DataHub,
                EntityType = typeof(TDataHubEntity).Name,
                EntityId = updatedEntity.id,
                Data = JObject.FromObject(updatedEntity)
            };
        }

        var action = _updateDataHubEntityAction(AgentServices, UpdateFunc, stashTo);
        ScenarioBuilder.Enqueue(action);
        return this;
    }

    public DataHubAgent UpdateDataHubEntity<TDataHubEntity>(string fromStash, string property, JToken newValue, string stashTo) where TDataHubEntity : DataHubEntity
    {
        UpdateEntityRequest UpdateFunc(object currentObject, Dictionary<string, object?> stash)
        {
            var stashedEntity = stash[fromStash]!.ToObject<TDataHubEntity>()!;
            var dhEntity = JObject.FromObject(stashedEntity);
            dhEntity[property] = newValue;
            return new UpdateEntityRequest()
            {
                DataSource = DataSources.DataHub,
                EntityType = typeof(TDataHubEntity).Name,
                EntityId = stashedEntity.id,
                Data = dhEntity
            };
        }

        var action = _updateDataHubEntityAction(AgentServices, UpdateFunc, stashTo);
        ScenarioBuilder.Enqueue(action);
        return this;
    }

    public DataHubAgent UpdateDataHubEntity<TDataHubEntity>(string fromStash, string property, Func<object, Dictionary<string, object?>, JToken> propertyFunc, string stashTo) where TDataHubEntity : DataHubEntity
    {
        UpdateEntityRequest UpdateFunc(object currentObject, Dictionary<string, object?> stash)
        {
            var stashedEntity = stash[fromStash]!.ToObject<TDataHubEntity>()!;
            var dhEntity = JObject.FromObject(stashedEntity);

            dhEntity[property] = propertyFunc(currentObject, stash);
            return new UpdateEntityRequest()
            {
                DataSource = DataSources.DataHub,
                EntityType = typeof(TDataHubEntity).Name,
                EntityId = stashedEntity.id,
                Data = dhEntity
            };
        }

        var action = _updateDataHubEntityAction(AgentServices, UpdateFunc, stashTo);
        ScenarioBuilder.Enqueue(action);
        return this;
    }

    public DataHubAgent UpdateDataHubEntity<TDataHubEntity>(string entityId, Func<TDataHubEntity, TDataHubEntity> modifierFunc, string stashTo) where TDataHubEntity : DataHubEntity
    {
        UpdateEntityRequest UpdateFunc(object currentObject, Dictionary<string, object?> stash)
        {
            var dataHubClient = AgentServices.GetRequiredService<IDataHubClient>();
            var getEntitiesResponse = dataHubClient.PostRequestAsync<GetDataHubEntityRequest, GetDataHubEntityResponse>(new GetDataHubEntityRequest()
            {
                EntityType = typeof(TDataHubEntity).Name,
                EntityId = entityId
            }, CancellationToken.None).Result;

            var currentEntity = getEntitiesResponse.Entity.ToObject<TDataHubEntity>()!;
            var updatedEntity = modifierFunc(currentEntity);

            return new UpdateEntityRequest()
            {
                DataSource = DataSources.DataHub,
                EntityType = typeof(TDataHubEntity).Name,
                EntityId = entityId,
                Data = JObject.FromObject(updatedEntity)
            };
        }

        var action = _updateDataHubEntityAction(AgentServices, UpdateFunc, stashTo);
        ScenarioBuilder.Enqueue(action);
        return this;
    }


    #endregion

    #region CreateDataHubEntity

    private readonly Func<IServiceProvider, Func<object, Dictionary<string, object?>, CreateEntityRequest>, string?, Type, Func<object, Dictionary<string, object?>, Task<ScenarioActionResult>>> _createDataHubEntityAction = (services, requestFunc, stashTo, dataHubType) =>
    {
        return async (currentObject, stash) =>
        {
            var dataHubClient = services.GetRequiredService<IDataHubClient>();
            var request = requestFunc(currentObject, stash);
            var response = await dataHubClient.PostRequestAsync<CreateEntityRequest, CreateEntityResponse>(request, CancellationToken.None);

            if (!string.IsNullOrEmpty(stashTo))
            {
                stash[stashTo] = response.ResultingEntity?.ToObject(dataHubType);
                stash[$"{stashTo}_createResponse"] = response;
            }

            return new ScenarioActionResult() { CurrentObject = response, Outputs = stash };
        };
    };

    public DataHubAgent CreateDataHubEntity<TDataHubEntity>(string stashTo, string? fromStash = null) where TDataHubEntity : DataHubEntity
    {
        CreateEntityRequest CreateFunc(object currentObject, Dictionary<string, object?> stash)
        {
            var stashedEntity = fromStash == null ? currentObject.ToObject<TDataHubEntity>() : stash[fromStash]?.ToObject<TDataHubEntity>();
            if (stashedEntity == null) throw new Exception("Stashed entity not found");

            var dhEntity = JObject.FromObject(stashedEntity);

            return new CreateEntityRequest()
            {
                DataSource = DataSources.DataHub,
                EntityType = typeof(TDataHubEntity).Name,
                EntityId = stashedEntity.id,
                Data = dhEntity
            };
        }

        var action = _createDataHubEntityAction(AgentServices, CreateFunc, stashTo, typeof(TDataHubEntity));
        ScenarioBuilder.Enqueue(action);
        return this;
    }

    public DataHubAgent CreateDataHubEntity<TDataHubEntity>(TDataHubEntity entity, string? stashTo = null) where TDataHubEntity : DataHubEntity
    {
        CreateEntityRequest CreateFunc(object currentObject, Dictionary<string, object?> stash)
        {
            var dhEntity = JObject.FromObject(entity);

            return new CreateEntityRequest()
            {
                DataSource = DataSources.DataHub,
                EntityType = typeof(TDataHubEntity).Name,
                EntityId = entity.id,
                Data = dhEntity
            };
        }

        var action = _createDataHubEntityAction(AgentServices, CreateFunc, stashTo, typeof(TDataHubEntity));
        ScenarioBuilder.Enqueue(action);
        return this;
    }

    public DataHubAgent CreateDataHubEntity<TDataHubEntity>(TDataHubEntity entity, Func<TDataHubEntity, TDataHubEntity> modifierFunc, string? stashTo = null) where TDataHubEntity : DataHubEntity
    {
        var modifiedEntity = modifierFunc(entity);
        CreateEntityRequest CreateFunc(object currentObject, Dictionary<string, object?> stash)
        {
            var entityData = JObject.FromObject(modifiedEntity);

            return new CreateEntityRequest()
            {
                DataSource = DataSources.DataHub,
                EntityType = typeof(TDataHubEntity).Name,
                EntityId = entity.id,
                Data = entityData
            };
        }

        var action = _createDataHubEntityAction(AgentServices, CreateFunc, stashTo, typeof(TDataHubEntity));
        ScenarioBuilder.Enqueue(action);
        return this;
    }

    public DataHubAgent CreateDataHubEntity<TDataHubEntity>(Func<TDataHubEntity> entityFunc, string? stashTo = null) where TDataHubEntity : DataHubEntity
    {
        CreateEntityRequest CreateFunc(object currentObject, Dictionary<string, object?> stash)
        {
            var dhEntity = entityFunc();

            return new CreateEntityRequest()
            {
                DataSource = DataSources.DataHub,
                EntityType = typeof(TDataHubEntity).Name,
                EntityId = dhEntity.id,
                Data = JObject.FromObject(dhEntity)
            };
        }

        var action = _createDataHubEntityAction(AgentServices, CreateFunc, stashTo, typeof(TDataHubEntity));
        ScenarioBuilder.Enqueue(action);
        return this;
    }

    public DataHubAgent CreateDataHubEntity<TDataHubEntity>(Func<object, Dictionary<string, object?>, TDataHubEntity> entityFunc, string? stashTo = null) where TDataHubEntity : DataHubEntity
    {
        CreateEntityRequest CreateFunc(object currentObject, Dictionary<string, object?> stash)
        {
            var dhEntity = entityFunc(currentObject, stash);

            return new CreateEntityRequest()
            {
                DataSource = DataSources.DataHub,
                EntityType = typeof(TDataHubEntity).Name,
                EntityId = dhEntity.id,
                Data = JObject.FromObject(dhEntity)
            };
        }

        var action = _createDataHubEntityAction(AgentServices, CreateFunc, stashTo, typeof(TDataHubEntity));
        ScenarioBuilder.Enqueue(action);
        return this;
    }

    #endregion

    #region CreateDataHubEntities

    private Func<IServiceProvider, Func<object, Dictionary<string, object?>, CreateEntitiesRequest>, string?, Func<object, Dictionary<string, object?>, Task<ScenarioActionResult>>> _createDataHubEntitiesAction<TDataHubType>() where TDataHubType : DataHubEntity
    {
        return (services, requestFunc, stashTo) =>
        {
            return async (currentObject, stash) =>
            {
                var dataHubClient = services.GetRequiredService<IDataHubClient>();
                var request = requestFunc(currentObject, stash);
                var response = await dataHubClient.PostRequestAsync<CreateEntitiesRequest, CreateEntitiesResponse>(request, CancellationToken.None);

                if (!string.IsNullOrEmpty(stashTo))
                {
                    stash[stashTo] = response.Results.Select(s =>
                    {
                        var ret = s.ResultingEntity.ToObject<TDataHubType>();
                        return ret;
                    }).ToList();
                    stash[$"{stashTo}_createResponse"] = response;
                }

                return new ScenarioActionResult() { CurrentObject = response, Outputs = stash };
            };
        };
    }

    public DataHubAgent CreateDataHubEntities<TDataHubEntity>(string stashTo, string? fromStash = null) where TDataHubEntity : DataHubEntity
    {
        CreateEntitiesRequest CreateFunc(object currentObject, Dictionary<string, object?> stash)
        {
            var stashedEntities = fromStash == null ? currentObject.ToObject<List<TDataHubEntity>>() : stash[fromStash]!.ToObject<List<TDataHubEntity>>();

            return new CreateEntitiesRequest()
            {
                Requests = (stashedEntities ?? []).Select(entity => new CreateEntityRequest()
                {
                    DataSource = DataSources.DataHub,
                    EntityType = typeof(TDataHubEntity).Name,
                    EntityId = entity.id,
                    Data = JObject.FromObject(entity)
                }).ToList()
            };
        }

        var action = _createDataHubEntitiesAction<TDataHubEntity>()(AgentServices, CreateFunc, stashTo);
        ScenarioBuilder.Enqueue(action);
        return this;
    }

    public DataHubAgent CreateDataHubEntities<TDataHubEntity>(List<TDataHubEntity> entities, string? stashTo = null) where TDataHubEntity : DataHubEntity
    {
        CreateEntitiesRequest CreateFunc(object currentObject, Dictionary<string, object?> stash)
        {
            return new CreateEntitiesRequest()
            {
                Requests = entities.Select(entity => new CreateEntityRequest()
                {
                    DataSource = DataSources.DataHub,
                    EntityType = typeof(TDataHubEntity).Name,
                    EntityId = entity.id,
                    Data = JObject.FromObject(entity)
                }).ToList()
            };
        }

        var action = _createDataHubEntitiesAction<TDataHubEntity>()(AgentServices, CreateFunc, stashTo);
        ScenarioBuilder.Enqueue(action);
        return this;
    }

    #endregion
}
