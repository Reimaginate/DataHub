using System.CommandLine.NamingConventionBinder;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using Reimaginate.CLI.Base.Attributes;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Delete.Entities;

[Argument("where", required: true)]
public class DeleteEntitiesWhereCommand : SubCommand<DeleteEntitiesCommand>
{
    public DeleteEntitiesWhereCommand(IServiceProvider serviceProvider) : base("where", serviceProvider)
    {
        Handler = CommandHandler.Create(HandleCommand);

    }

    public async Task<int> HandleCommand(string where, CancellationToken cancellationToken = default)
    {
        var adminApi = ServiceProvider.GetRequiredService<ICLIApi>();

        var response = await adminApi.PostAdminMessage<GetEntitiesResponse>(new SerializedRequest()
        {
            RequestType = nameof(GetEntitiesWhereRequest),
            Data = JsonConvert.SerializeObject(new GetEntitiesWhereRequest()
            {
                WhereClause = where,
                Select = "x.id",
                PageSize = -1
            })
        }, cancellationToken);


        while (response.MoreResultsAvailable)
        {
            response = await adminApi.PostAdminMessage<GetEntitiesResponse>(new SerializedRequest()
            {
                RequestType = nameof(GetEntitiesWhereRequest),
                Data = JsonConvert.SerializeObject(new GetEntitiesWhereRequest()
                {
                    WhereClause = where,
                    Select = "x.id",
                    PageSize = -1,
                    ContinuationToken = response.ContinuationToken
                })
            }, cancellationToken);
        }

        return 1;
    }
}