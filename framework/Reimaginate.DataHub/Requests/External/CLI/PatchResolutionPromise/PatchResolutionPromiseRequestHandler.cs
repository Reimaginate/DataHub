using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.DataAccess.Commands.UpsertCosmosDocuments;
using Reimaginate.DataHub.DataAccess.Queries.GetCosmosDocuments;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.DataServices;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.CLI.PatchResolutionPromise;

public class PatchResolutionPromiseRequestHandler(IMediator mediator) : IHandler<PatchResolutionPromiseRequest, PatchResolutionPromiseResponse>
{
    private const string PatchedStatus = "Patched";
    private const string DryRunStatus = "DryRun";
    private const string UnchangedStatus = "Unchanged";
    private const string FailedStatus = "Failed";

    public async Task<PatchResolutionPromiseResponse> HandleAsync(PatchResolutionPromiseRequest request, CancellationToken cancellationToken)
    {
        var promise = await LoadPromise(request.PromiseId, cancellationToken);
        if (promise == null)
        {
            return new PatchResolutionPromiseResponse
            {
                PromiseId = request.PromiseId,
                Success = false,
                Status = FailedStatus,
                Reason = "Resolution promise was not found."
            };
        }

        var original = JObject.FromObject(promise);
        var updated = (JObject)original.DeepClone();
        var failures = ApplyOperations(updated, request.Operations);
        if (failures.Count > 0)
        {
            return new PatchResolutionPromiseResponse
            {
                PromiseId = request.PromiseId,
                Success = false,
                Status = FailedStatus,
                Reason = string.Join("; ", failures),
                Result = ResolutionPromiseResultMapper.ToResult(promise)
            };
        }

        var changed = !JToken.DeepEquals(original, updated);
        var updatedPromise = updated.ToObject<ResolutionPromise>();
        if (updatedPromise == null)
        {
            return new PatchResolutionPromiseResponse
            {
                PromiseId = request.PromiseId,
                Success = false,
                Status = FailedStatus,
                Reason = "Patched document could not be converted to a resolution promise.",
                Result = ResolutionPromiseResultMapper.ToResult(promise)
            };
        }

        if (changed && !request.DryRun)
        {
            var upsertResponse = (await mediator.SendAsync(new UpsertCosmosDocumentsCommand<ResolutionPromise>
            {
                Documents = [updatedPromise]
            }, cancellationToken)) switch { { IsT1: true } result => throw result.AsT1, { AsT0: var value } => value };

            var failure = upsertResponse.Failures?.FirstOrDefault();
            if (failure != null)
            {
                return new PatchResolutionPromiseResponse
                {
                    PromiseId = request.PromiseId,
                    Success = false,
                    Changed = changed,
                    Status = FailedStatus,
                    Reason = failure.Error?.Message,
                    Result = ResolutionPromiseResultMapper.ToResult(updatedPromise)
                };
            }

            updatedPromise = upsertResponse.Successes?.FirstOrDefault() ?? updatedPromise;
        }

        return new PatchResolutionPromiseResponse
        {
            PromiseId = request.PromiseId,
            Success = true,
            Changed = changed,
            Status = request.DryRun ? DryRunStatus : changed ? PatchedStatus : UnchangedStatus,
            Reason = request.DryRun ? "Matched but not patched." : changed ? "Patch applied." : "Patch made no changes.",
            Result = ResolutionPromiseResultMapper.ToResult(updatedPromise)
        };
    }

    private async Task<ResolutionPromise> LoadPromise(string promiseId, CancellationToken cancellationToken)
    {
        var response = (await mediator.TrySend(new GetCosmosDocumentsQuery<ResolutionPromise>
        {
            WhereClause = $"x.{nameof(ResolutionPromise.id)} = @promiseId",
            Parameters = [new QueryParameter("promiseId", promiseId)],
            PageSize = 1
        }, cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var value } => value };

        return response.Results?.FirstOrDefault();
    }

    private static List<string> ApplyOperations(JObject document, IEnumerable<Patch> operations)
    {
        var failures = new List<string>();
        foreach (var operation in operations)
        {
            try
            {
                ApplyOperation(document, operation);
            }
            catch (Exception exception)
            {
                failures.Add($"{operation.Path}: {exception.Message}");
            }
        }

        return failures;
    }

    private static void ApplyOperation(JObject document, Patch operation)
    {
        switch (operation.Operation?.Trim().ToLowerInvariant())
        {
            case "set":
                SetValue(document, operation.Path, operation.Value);
                break;
            case "remove":
                RemoveValue(document, operation.Path);
                break;
            default:
                throw new ArgumentException($"{operation.Operation} not supported");
        }
    }

    private static void SetValue(JObject document, string path, JToken value)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path is required.");
        }

        var token = document.SelectToken(path);
        if (token != null)
        {
            token.Replace(value ?? JValue.CreateNull());
            return;
        }

        var pathParts = path.Split('.');
        var parent = document;
        foreach (var pathPart in pathParts.Take(pathParts.Length - 1))
        {
            if (parent[pathPart] is not JObject child)
            {
                child = new JObject();
                parent[pathPart] = child;
            }

            parent = child;
        }

        parent[pathParts.Last()] = value ?? JValue.CreateNull();
    }

    private static void RemoveValue(JObject document, string path)
    {
        var token = document.SelectToken(path);
        if (token?.Parent?.Type == JTokenType.Array)
        {
            ((JArray)token.Parent).Remove(token);
            return;
        }

        token?.Parent?.Remove();
    }
}
