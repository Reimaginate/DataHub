using Reimaginate.DataHub.DataAccess.Queries.GetCosmosDocuments;
using Reimaginate.DataServices.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using Reimaginate.DataServices;
using Reimaginate.Mediator;


namespace Reimaginate.DataHub.Services.EntityConfig
{
    public interface IEntityConfigService
    {
        Task<SharedModels.Core.EntityConfig> GetEntityConfig(string entityType, CancellationToken cancellationToken);
    }

    public class EntityConfigService(IMediator mediator) : IEntityConfigService
    {
        private readonly MemoryCache _cache = new(new MemoryCacheOptions());


        public async Task<SharedModels.Core.EntityConfig> GetEntityConfig(string entityType, CancellationToken cancellationToken)
        {
            if (_cache.TryGetValue(entityType, out SharedModels.Core.EntityConfig entityConfig))
            {
                return entityConfig;
            }
         
            var results = new List<SharedModels.Core.EntityConfig>();
            
            var response = (await mediator.TrySend(new GetCosmosDocumentsQuery<SharedModels.Core.EntityConfig>()
            {
                WhereClause = "x.EntityType = @entityType",
                PageSize = 1000,
                Parameters = [new QueryParameter("entityType", entityType)]
            }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
            results.AddRange(response.Results);

            while (response.MoreResultsAvailable)
            {
                response = (await mediator.TrySend(new GetCosmosDocumentsQuery<SharedModels.Core.EntityConfig>()
                {
                    WhereClause = "x.EntityType = @entityType",
                    PageSize = 1000,
                    ContinuationToken = response.ContinuationToken,
                    Parameters = [new QueryParameter("entityType", entityType)]
                }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };
                results.AddRange(response.Results);
            }


            if (results.Count > 1) throw new Exception($"Multiple entity schemas found for entity type {entityType}");

            entityConfig = results.FirstOrDefault();
            if (entityConfig == null)
            {
                return null;
            }

            var expirationToken = new CancellationChangeToken(new CancellationTokenSource(TimeSpan.FromMinutes(15)).Token);
            var cacheOptions = new MemoryCacheEntryOptions().AddExpirationToken(expirationToken);
            _cache.Set(entityType, entityConfig, cacheOptions);

            return entityConfig;
        }
    }
}
