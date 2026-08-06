using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Reimaginate.DataHub.DataAccess.Commands.DeleteCosmosDocuments;
using Reimaginate.DataHub.DataAccess.Queries;
using Reimaginate.DataHub.DataAccess.Queries.GetCosmosDocuments;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.DataServices;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.CLI.DeleteResolutionPromises;

public class DeleteResolutionPromisesRequestHandler(IMediator mediator) : IHandler<DeleteResolutionPromisesRequest, DeleteResolutionPromisesResponse>
{
    private const int MaxDeletePageSize = 500;
    private const int DeleteBatchSize = 500;
    private const string DeletedStatus = "Deleted";
    private const string DryRunStatus = "DryRun";
    private const string FailedStatus = "Failed";

    public async Task<DeleteResolutionPromisesResponse> HandleAsync(DeleteResolutionPromisesRequest request, CancellationToken cancellationToken)
    {
        var query = CreateQuery(request);
        var results = new List<DeleteResolutionPromiseResult>();
        var matchedCount = 0;
        var deletedCount = 0;
        var failedCount = 0;
        var capPageSize = request.PromiseIds is not { Count: > 0 };
        var page = await LoadPage(query, request.PageSize, ContinuationTokenForQuery(request), capPageSize, cancellationToken);
        var promises = (page.Results ?? [])
            .Where(HasPromiseId)
            .DistinctBy(promise => promise.id)
            .ToList();

        matchedCount += promises.Count;

        if (request.DryRun)
        {
            results.AddRange(promises.Select(promise => ResolutionPromiseResultMapper.ToDeleteResult(promise, DryRunStatus, "Matched but not deleted.")));
        }
        else if (promises.Any())
        {
            foreach (var batch in promises.Chunk(DeleteBatchSize))
            {
                var deleteResponse = (await mediator.SendAsync(new DeleteCosmosDocumentsCommand<ResolutionPromise>
                {
                    Documents = batch.ToList()
                }, cancellationToken)) switch { { IsT1: true } result => throw result.AsT1, { AsT0: var value } => value };

                var successfulPromises = (deleteResponse.Successes ?? [])
                    .Where(HasPromiseId)
                    .ToList();
                var failedPromises = (deleteResponse.Failures ?? [])
                    .Select(failure => new { Promise = failure?.Item, Error = failure?.Error, Reason = failure?.Error?.Message })
                    .Where(failure => HasPromiseId(failure.Promise))
                    .ToList();
                var alreadyDeletedPromises = failedPromises
                    .Where(failure => IsAlreadyDeletedFailure(failure.Error))
                    .ToList();
                var actualFailedPromises = failedPromises
                    .Where(failure => !IsAlreadyDeletedFailure(failure.Error))
                    .ToList();

                deletedCount += successfulPromises.Count + alreadyDeletedPromises.Count;
                failedCount += actualFailedPromises.Count;
                results.AddRange(successfulPromises.Select(promise => ResolutionPromiseResultMapper.ToDeleteResult(promise, DeletedStatus)));
                results.AddRange(alreadyDeletedPromises.Select(failure => ResolutionPromiseResultMapper.ToDeleteResult(failure.Promise, DeletedStatus, "Already deleted.")));
                results.AddRange(actualFailedPromises.Select(failure => ResolutionPromiseResultMapper.ToDeleteResult(failure.Promise, FailedStatus, failure.Reason)));
            }
        }

        return new DeleteResolutionPromisesResponse
        {
            MatchedCount = matchedCount,
            DeletedCount = deletedCount,
            FailedCount = failedCount,
            ContinuationToken = page.ContinuationToken,
            MoreResultsAvailable = page.MoreResultsAvailable,
            Results = results
        };
    }

    private static PromiseQuery CreateQuery(DeleteResolutionPromisesRequest request)
    {
        var parameters = new List<QueryParameter>();
        if (request.PromiseIds is { Count: > 0 })
        {
            var idParameterNames = DataHubQueryParameterMapper.AddIndexedParameters(request.PromiseIds.Distinct().ToList(), "promiseId", parameters);
            return new PromiseQuery($"x.{nameof(ResolutionPromise.id)} in ({string.Join(",", idParameterNames)})", parameters);
        }

        parameters.AddRange(DataHubQueryParameterMapper.ToDataServiceParameters(request.Parameters));
        return new PromiseQuery(request.WhereClause, parameters);
    }

    private async Task<PagedResults<ResolutionPromise>> LoadPage(PromiseQuery query, int pageSize, string continuationToken, bool capPageSize, CancellationToken cancellationToken)
    {
        return (await mediator.TrySend(new GetCosmosDocumentsQuery<ResolutionPromise>
        {
            WhereClause = query.WhereClause,
            Parameters = query.Parameters,
            PageSize = capPageSize ? Math.Clamp(pageSize, 1, MaxDeletePageSize) : pageSize,
            ContinuationToken = continuationToken
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var value } => value };
    }

    private sealed record PromiseQuery(string WhereClause, IReadOnlyCollection<QueryParameter> Parameters);

    private static string ContinuationTokenForQuery(DeleteResolutionPromisesRequest request)
    {
        return request.DryRun ? request.ContinuationToken : null;
    }

    private static bool HasPromiseId(ResolutionPromise promise)
    {
        return !string.IsNullOrWhiteSpace(promise?.id);
    }

    private static bool IsAlreadyDeletedFailure(Exception exception)
    {
        return exception switch
        {
            CosmosException { StatusCode: HttpStatusCode.NotFound } => true,
            { Message: { } message } when message.Contains("not found", StringComparison.OrdinalIgnoreCase) => true,
            { Message: { } message } when message.Contains("404", StringComparison.OrdinalIgnoreCase) => true,
            _ => false
        };
    }
}
