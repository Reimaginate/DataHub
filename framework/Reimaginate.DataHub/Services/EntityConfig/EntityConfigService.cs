using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Reimaginate.DataHub.DataAccess.Queries.GetCosmosDocuments;
using Reimaginate.DataServices;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Services.EntityConfig;

public interface IEntityConfigService
{
    Task<SharedModels.Core.EntityConfig> GetEntityConfig(string entityType, CancellationToken cancellationToken);

    // Tracking-only readers can tolerate bounded discovery of a newly created configuration.
    // Existing implementations retain their original behaviour through this default overload.
    Task<SharedModels.Core.EntityConfig> GetEntityConfig(string entityType, CancellationToken cancellationToken, bool cacheMissing)
        => GetEntityConfig(entityType, cancellationToken);
}

public class EntityConfigService : IEntityConfigService, IDisposable
{
    private readonly IMediator _mediator;
    private readonly TimeProvider _timeProvider;
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly ConcurrentDictionary<string, Lazy<Task<SharedModels.Core.EntityConfig>>> _loads = new();
    private sealed record CachedConfig(SharedModels.Core.EntityConfig Value, DateTimeOffset ExpiresAt);

    public EntityConfigService(IMediator mediator) : this(mediator, TimeProvider.System) { }

    internal EntityConfigService(IMediator mediator, TimeProvider timeProvider)
    {
        _mediator = mediator;
        _timeProvider = timeProvider;
    }

    public Task<SharedModels.Core.EntityConfig> GetEntityConfig(string entityType, CancellationToken cancellationToken)
        => GetEntityConfig(entityType, cancellationToken, cacheMissing: false);

    public async Task<SharedModels.Core.EntityConfig> GetEntityConfig(string entityType, CancellationToken cancellationToken, bool cacheMissing)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_cache.TryGetValue(entityType, out CachedConfig cached) && cached.ExpiresAt > _timeProvider.GetUtcNow() && (cached.Value != null || cacheMissing))
            return cached.Value;

        var load = _loads.GetOrAdd(entityType, key => new Lazy<Task<SharedModels.Core.EntityConfig>>(
            () => LoadAndCache(key), LazyThreadSafetyMode.ExecutionAndPublication));
        // Cancellation belongs to this waiter, not to other operations sharing the fetch.
        return await load.Value.WaitAsync(cancellationToken);
    }

    private async Task<SharedModels.Core.EntityConfig> LoadAndCache(string entityType)
    {
        try
        {
            // Another fetch may have completed between the caller's check and insertion.
            if (_cache.TryGetValue(entityType, out CachedConfig cached) && cached.ExpiresAt > _timeProvider.GetUtcNow() && cached.Value != null)
                return cached.Value;

            var results = new List<SharedModels.Core.EntityConfig>();
            string continuationToken = null;
            do
            {
                var response = (await _mediator.TrySend(new GetCosmosDocumentsQuery<SharedModels.Core.EntityConfig>
                {
                    WhereClause = "x.EntityType = @entityType",
                    PageSize = 1000,
                    ContinuationToken = continuationToken,
                    Parameters = [new QueryParameter("entityType", entityType)]
                }, CancellationToken.None)) switch
                {
                    { Item2: { } exception } => throw exception,
                    { Item1: var value } => value
                };
                results.AddRange(response.Results);
                continuationToken = response.MoreResultsAvailable ? response.ContinuationToken : null;
            } while (continuationToken != null);

            if (results.Count > 1) throw new Exception($"Multiple entity schemas found for entity type {entityType}");
            var config = results.FirstOrDefault();
            var lifetime = config == null ? TimeSpan.FromMinutes(1) : TimeSpan.FromMinutes(15);
            _cache.Set(entityType, new CachedConfig(config, _timeProvider.GetUtcNow() + lifetime), lifetime);
            return config;
        }
        finally
        {
            // Failures are retriable and are never cached as an absent configuration.
            _loads.TryRemove(entityType, out _);
        }
    }

    public void Dispose() => _cache.Dispose();
}
