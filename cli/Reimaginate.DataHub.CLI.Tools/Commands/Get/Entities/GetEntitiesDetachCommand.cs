using System.CommandLine.NamingConventionBinder;
using Reimaginate.CLI.Base.Attributes;
using Reimaginate.DataHub.CLI.Tools.Helpers;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Get.Entities;

[Argument("where", required: true)]
[Argument("dataSource", required: true)]
public class GetEntitiesDetachCommand : SubCommand<GetEntitiesCommand>
{
    public GetEntitiesDetachCommand(IServiceProvider serviceProvider) : base("detach", "Detach entities from a data source", serviceProvider)
    {
        Handler = CommandHandler.Create(HandleCommand);
    }

    public Task<int> HandleCommand(string where, string dataSource, CancellationToken cancellationToken = default)
    {
        return CliHelpers.DetachFromDataSourceAsync(
            new GetEntitiesWhereRequest { WhereClause = where, PageSize = 5000 },
            dataSource,
            ServiceProvider,
            cancellationToken);
    }
}
