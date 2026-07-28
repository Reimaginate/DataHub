using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.DataAccess.Commands.UpsertCosmosDocuments;
using Reimaginate.DataHub.DataAccess.Commands.UpsertDataHubEntities;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.Requests.Internal.ProcessRegisterAlternateKey;
using Reimaginate.DataHub.Services.TimeService;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.ProcessRegisterAlternateKeys;

public class ProcessRegisterAlternateKeysRequestHandler(IMediator mediator, ITimeService timeService) : IHandler<ProcessRegisterAlternateKeysRequest, ProcessRegisterAlternateKeysResponse>
{
    public async Task<ProcessRegisterAlternateKeysResponse> HandleAsync(ProcessRegisterAlternateKeysRequest request, CancellationToken cancellationToken)
    {
        var responses = request.Requests.ToDictionary(k => k, v => new ProcessRegisterAlternateKeyResponse()
        {
            Success = true
        });

        var groupedByEntityType = request.Requests.GroupBy(g => g.EntityType);
        foreach (var group in groupedByEntityType)
        {
            var changeTrackingEntries = new ConcurrentBag<ChangeTrackingEntry>();
            var updatedEntities = new ConcurrentBag<JObject>();

            var dataHubEntityIds = group.Select(s => s.DataHubEntityId).ToList();
            var getDataHubEntitiesResponse = (await mediator.TrySend(new GetDataHubEntitiesByIdRequest()
            {
                EntityType = group.Key,
                EntityIds = dataHubEntityIds
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

            var dataHubEntitiesDic = getDataHubEntitiesResponse.Results.ToDictionary(k => k.Value<string>(nameof(DataHubEntity.id)), v => v);

            Parallel.ForEach(group, new ParallelOptions()
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Environment.ProcessorCount
            }, (req, _) =>
            {
                var dataHubEntity = dataHubEntitiesDic[req.DataHubEntityId];
                if (!dataHubEntity.ContainsKey(nameof(DataHubEntity.alternateKeys)))
                {
                    dataHubEntity.Add(nameof(DataHubEntity.alternateKeys), new JArray());
                }

                var alternateKeys = dataHubEntity.Value<JArray>(nameof(DataHubEntity.alternateKeys));
                
                if (req.ReplaceSameDataSource)
                {
                    var existingDataSourceKeys = alternateKeys.Where(ak => ak.Value<string>(nameof(AlternateKey.Key)).Split('.').First() == req.Key.Split('.').First()).ToList();
                    existingDataSourceKeys.ForEach(e=> alternateKeys.Remove(e));
                }

                if (req.Replace)
                {
                    var existingKeys = alternateKeys.Where(ak => ak.Value<string>(nameof(AlternateKey.Key)) == req.Key).ToList();
                    existingKeys.ForEach(e => alternateKeys.Remove(e));
                }

                var updatedAlternateKeys = (JArray)alternateKeys.DeepClone();

                if (!alternateKeys.Any(ak => ak.Value<string>(nameof(AlternateKey.Key)) == req.Key && ak.Value<string>(nameof(AlternateKey.Value)) == req.SourceEntityId))
                {
                    updatedAlternateKeys.Add(new JObject()
                    {
                        [nameof(AlternateKey.Key)] = req.Key,
                        [nameof(AlternateKey.Value)] = req.SourceEntityId
                    });
                }

                var altKeyDiffs = ChangeTrackingHelper.DiffIgnoringEquivalentValueTokens(alternateKeys, updatedAlternateKeys);
                if (altKeyDiffs == null) return;

                if (!req.Untracked)
                {
                    var alternateKeyPatch = new JObject
                    {
                        [nameof(DataHubEntity.alternateKeys)] = altKeyDiffs
                    };

                    var trackedEntityLastUpdated = dataHubEntity.DateTimeOffsetValue(nameof(DataHubEntity.lastUpdated));
                    var now = timeService.Now();

                    var cte = new ChangeTrackingEntry()
                    {
                        EntityId = dataHubEntity.DataHubEntityId(),
                        EntityType = dataHubEntity.DataHubEntityType(),
                        DataSource = DataSources.DataHub,
                        EntryType = ChangeTrackingEntryTypes.Update,
                        Timestamp = trackedEntityLastUpdated != null && trackedEntityLastUpdated.Value > now ? trackedEntityLastUpdated.Value : now,
                        Data = alternateKeyPatch
                    };

                    changeTrackingEntries.Add(cte);
                }

                var existingProp = dataHubEntity[nameof(alternateKeys)];
                existingProp!.Replace(updatedAlternateKeys);
                updatedEntities.Add(dataHubEntity);
            });

            if (changeTrackingEntries.Any())
            {
                var addChangeTrackingRecordsCommand = new UpsertCosmosDocumentsCommand<ChangeTrackingEntry>()
                {
                    Documents = changeTrackingEntries.ToList()
                };

                var addTrackingEntriesResponse = (await mediator.TrySend(addChangeTrackingRecordsCommand, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
                addTrackingEntriesResponse.Failures.ForEach(fail =>
                {
                    var response = responses.First(w => w.Key.DataHubEntityId == fail.Item.EntityId).Value;
                    response.Success = false;
                    response.Exception = fail.Error?.Message;
                });

            }

            if (updatedEntities.Any())
            {
                var updateEntitiesResponse = (await mediator.TrySend(new UpsertDataHubEntitiesCommand()
                {
                    Entities = updatedEntities.ToList()
                }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
                updateEntitiesResponse.Failures.ForEach(fail =>
                {
                    var response = responses.First(w => w.Key.DataHubEntityId == fail.Item.Value<string>(nameof(DataHubEntity.id))).Value;
                    response.Success = false;
                    response.Exception = fail.Error?.Message;
                });
            }
        }


        return new ProcessRegisterAlternateKeysResponse()
        {
            Responses = responses.Values.ToList()
        };
    }
}
