using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.Models;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataServices;
using Reimaginate.DataServices.Cosmos;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.DataAccess.Commands.DeleteCosmosDocuments;

public class DeleteCosmosDocumentsCommandHandler<T>(IServiceProvider serviceProvider) : IHandler<DeleteCosmosDocumentsCommand<T>, DeleteCosmosDocumentsResponse<T>>
    where T : CosmosDocument
{
    private readonly IPartitionedDataService<T> _dataService = serviceProvider.GetRequiredService<IPartitionedDataService<T>>();
    private readonly CosmosDataServiceOptions<T> _dataServiceOptions = serviceProvider.GetService<CosmosDataServiceOptions<T>>();

    public async Task<DeleteCosmosDocumentsResponse<T>> HandleAsync(DeleteCosmosDocumentsCommand<T> request, CancellationToken cancellationToken)
    {
        var response = await _dataService.BulkDeleteItemsAsync(request.Documents, cancellationToken);
        var failures = (response.Failures ?? [])
            .Select(failure => new DeleteFailure(failure?.Item, failure?.Error))
            .Where(failure => HasDocumentId(failure.Document))
            .DistinctBy(failure => failure.Document.id)
            .ToList();
        var failureIds = failures.Select(failure => failure.Document.id).ToHashSet();
        var successes = (request.Documents ?? [])
            .Where(HasDocumentId)
            .Where(document => !failureIds.Contains(document.id))
            .DistinctBy(document => document.id)
            .ToList();

        if (failures.Any() && _dataServiceOptions != null)
        {
            var directDeleteSuccesses = await DeleteDirectlyWithCosmosSdk(failures.Select(failure => failure.Document).ToList(), cancellationToken);
            successes.AddRange(directDeleteSuccesses);
            var directDeleteSuccessIds = directDeleteSuccesses.Select(document => document.id).ToHashSet();
            failures = failures
                .Where(failure => !directDeleteSuccessIds.Contains(failure.Document.id))
                .ToList();

            var queriedDeleteSuccesses = await DeleteByQueryingActualCosmosPartitionKeys(failures, cancellationToken);
            successes.AddRange(queriedDeleteSuccesses);
            var queriedDeleteSuccessIds = queriedDeleteSuccesses.Select(document => document.id).ToHashSet();
            failures = failures
                .Where(failure => !queriedDeleteSuccessIds.Contains(failure.Document.id))
                .ToList();
        }

        if (failures.Any())
        {
            var fallbackDocuments = failures
                .Select(failure => failure.Document)
                .DistinctBy(document => document.id)
                .ToList();

            if (fallbackDocuments.Any())
            {
                try
                {
                    await _dataService.DeleteItemsAsync(fallbackDocuments, cancellationToken);
                    successes.AddRange(fallbackDocuments);
                    var fallbackDocumentIds = fallbackDocuments.Select(document => document.id).ToHashSet();
                    failures = failures
                        .Where(failure => !fallbackDocumentIds.Contains(failure.Document.id))
                        .ToList();
                }
                catch
                {
                }
            }
        }

        return new DeleteCosmosDocumentsResponse<T>()
        {
            Successes = successes
                .Where(HasDocumentId)
                .DistinctBy(document => document.id)
                .ToList(),
            Failures = failures
                .Where(failure => HasDocumentId(failure.Document))
                .Select(failure => new DataAccessFailure<T>(failure.Document, failure.Error))
                .ToList()
        };
    }

    private async Task<List<T>> DeleteDirectlyWithCosmosSdk(List<T> documents, CancellationToken cancellationToken)
    {
        var container = _dataServiceOptions.CosmosClient.GetContainer(_dataServiceOptions.DatabaseName, _dataServiceOptions.ContainerName);
        var successes = new List<T>();

        foreach (var document in documents)
        {
            var id = _dataServiceOptions.GetItemIdFunc?.Invoke(document) ?? document.id;
            var partitionKeysToTry = GetPartitionKeysToTry(document).ToList();

            foreach (var partitionKey in partitionKeysToTry)
            {
                try
                {
                    await container.DeleteItemAsync<T>(id, partitionKey, cancellationToken: cancellationToken);
                    successes.Add(document);
                    break;
                }
                catch (CosmosException ex) when (ex.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.BadRequest)
                {
                    // Try the next possible partition key representation.
                }
            }
        }

        return successes;
    }

    private async Task<List<T>> DeleteByQueryingActualCosmosPartitionKeys(List<DeleteFailure> failures, CancellationToken cancellationToken)
    {
        var container = _dataServiceOptions.CosmosClient.GetContainer(_dataServiceOptions.DatabaseName, _dataServiceOptions.ContainerName);
        var successes = new List<T>();

        foreach (var failure in failures.Where(failure => HasDocumentId(failure.Document)))
        {
            var document = failure.Document;
            foreach (var id in GetItemIdsToTry(document))
            {
                var query = new QueryDefinition("SELECT * FROM c WHERE c.id = @id")
                    .WithParameter("@id", id);
                using var iterator = container.GetItemQueryIterator<JObject>(query);

                while (iterator.HasMoreResults)
                {
                    foreach (var rawDocument in await iterator.ReadNextAsync(cancellationToken))
                    {
                        var rawId = rawDocument.Value<string>(nameof(DataDocument.id)) ?? id;
                        foreach (var partitionKey in GetPartitionKeysToTry(rawDocument))
                        {
                            try
                            {
                                await container.DeleteItemAsync<JObject>(rawId, partitionKey, cancellationToken: cancellationToken);
                                successes.Add(document);
                                break;
                            }
                            catch (CosmosException ex) when (ex.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.BadRequest)
                            {
                                // Try the next possible partition key representation.
                            }
                        }

                        if (successes.Any(success => success.id == document.id))
                        {
                            break;
                        }
                    }

                    if (successes.Any(success => success.id == document.id))
                    {
                        break;
                    }
                }

                if (successes.Any(success => success.id == document.id))
                {
                    break;
                }
            }
        }

        return successes;
    }

    private IEnumerable<PartitionKey> GetPartitionKeysToTry(T document)
    {
        var partitionKeyPaths = GetPartitionKeyPaths();
        var configuredPartitionKey = _dataServiceOptions.GetItemPartitionKeyFunc?.Invoke(document);

        foreach (var partitionKey in CreatePartitionKeys(configuredPartitionKey, partitionKeyPaths))
        {
            yield return partitionKey;
        }

        var propertyValues = GetPartitionKeyValuesFromProperties(document, partitionKeyPaths);
        if (propertyValues.Count == partitionKeyPaths.Count && propertyValues.All(value => value != null))
        {
            yield return CreatePartitionKey(propertyValues);
        }

        foreach (var partitionKeyValue in new[] { document.pk, document.id, string.Empty }.Where(key => key != null).Distinct())
        {
            foreach (var partitionKey in CreatePartitionKeys(partitionKeyValue, partitionKeyPaths))
            {
                yield return partitionKey;
            }
        }

        yield return PartitionKey.None;
    }

    private IEnumerable<PartitionKey> GetPartitionKeysToTry(JObject document)
    {
        var partitionKeyPaths = GetPartitionKeyPaths();
        var propertyValues = GetPartitionKeyValuesFromProperties(document, partitionKeyPaths);
        if (propertyValues.Count == partitionKeyPaths.Count && propertyValues.All(value => value != null))
        {
            yield return CreatePartitionKey(propertyValues);
        }

        foreach (var partitionKeyValue in new[] { GetPropertyValue(document, nameof(PartitionedDataDocument.pk)), GetPropertyValue(document, nameof(DataDocument.id)), string.Empty }.Where(key => key != null).Distinct())
        {
            foreach (var partitionKey in CreatePartitionKeys(partitionKeyValue, partitionKeyPaths))
            {
                yield return partitionKey;
            }
        }

        yield return PartitionKey.None;
    }

    private IEnumerable<string> GetItemIdsToTry(T document)
    {
        return new[] { _dataServiceOptions.GetItemIdFunc?.Invoke(document), document.id }
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct();
    }

    private IReadOnlyList<string> GetPartitionKeyPaths()
    {
        return (_dataServiceOptions.PartitionKey ?? string.Empty)
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static IEnumerable<PartitionKey> CreatePartitionKeys(string partitionKeyValue, IReadOnlyList<string> partitionKeyPaths)
    {
        if (partitionKeyValue == null)
        {
            yield break;
        }

        if (partitionKeyPaths.Count <= 1)
        {
            yield return new PartitionKey(partitionKeyValue);
            yield break;
        }

        var partitionKeyValues = partitionKeyValue.Split('/');
        if (partitionKeyValues.Length == partitionKeyPaths.Count)
        {
            yield return CreatePartitionKey(partitionKeyValues);
        }
    }

    private static List<string> GetPartitionKeyValuesFromProperties(T document, IReadOnlyList<string> partitionKeyPaths)
    {
        var documentType = typeof(T);
        return partitionKeyPaths
            .Select(path => documentType.GetProperty(path, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)?.GetValue(document)?.ToString())
            .ToList();
    }

    private static List<string> GetPartitionKeyValuesFromProperties(JObject document, IReadOnlyList<string> partitionKeyPaths)
    {
        return partitionKeyPaths
            .Select(path => GetPropertyValue(document, path))
            .ToList();
    }

    private static string GetPropertyValue(JObject document, string path)
    {
        var property = document.Properties()
            .FirstOrDefault(candidate => string.Equals(candidate.Name, path, StringComparison.OrdinalIgnoreCase));
        return property?.Value?.Type == JTokenType.Null ? null : property?.Value?.ToString();
    }

    private static PartitionKey CreatePartitionKey(IEnumerable<string> partitionKeyValues)
    {
        var builder = new PartitionKeyBuilder();

        foreach (var partitionKeyValue in partitionKeyValues)
        {
            builder.Add(partitionKeyValue);
        }

        return builder.Build();
    }

    private static bool HasDocumentId(T document)
    {
        return !string.IsNullOrWhiteSpace(document?.id);
    }

    private sealed record DeleteFailure(T Document, Exception Error);
}
