using Newtonsoft.Json;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Get;

public static class GetHelpers
{
    public static async Task WriteIdsToConsole(ICLIApi adminApi, string where, CancellationToken cancellationToken)
    {
        var firstWrite = true;

        await foreach (var response in FetchEntitiesWhereAsync(adminApi, where, cancellationToken))
        {
            if (response.Results?.Count > 0)
            {
                var ids = response.Results.Select(s => s.Value<string>(nameof(DataHubEntity.id)));
                var joinedIds = string.Join(" ", ids);

                if (!firstWrite)
                {
                    AnsiConsole.Write(" ");
                }

                AnsiConsole.Write(joinedIds);

                firstWrite = false;
            }

            if (!response.MoreResultsAvailable)
            {
                break;
            }
        }

    }

    public static async IAsyncEnumerable<GetEntitiesResponse> FetchEntitiesWhereAsync(ICLIApi adminApi, string where, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string? continuationToken = null;

        while (true)
        {
            var request = new GetEntitiesWhereRequest
            {
                WhereClause = where,
                PageSize = -1,
                ContinuationToken = continuationToken
            };

            var serializedRequest = new SerializedRequest
            {
                RequestType = nameof(GetEntitiesWhereRequest),
                Data = JsonConvert.SerializeObject(request)
            };

            var response = await adminApi.PostAdminMessage<GetEntitiesResponse>(serializedRequest, cancellationToken);
            yield return response;

            if (!response.MoreResultsAvailable)
            {
                break;
            }

            continuationToken = response.ContinuationToken;
        }
    }


}
