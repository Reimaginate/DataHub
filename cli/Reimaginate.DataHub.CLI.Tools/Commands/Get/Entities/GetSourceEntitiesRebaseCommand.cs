using System.CommandLine.NamingConventionBinder;
using Reimaginate.CLI.Base.Attributes;
using Reimaginate.DataHub.CLI.Tools.Helpers;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Get.Entities;

[Argument("where", required: true)]
[Argument("dataSource", required: true)]
[Argument("rebaseTo", required: true)]
public class GetSourceEntitiesRebaseCommand : SubCommand<GetEntitiesCommand>
{
    public GetSourceEntitiesRebaseCommand(IServiceProvider serviceProvider) : base("rebase-source", "Rebase source entities matched by a DataHub entity query", serviceProvider)
    {
        Handler = CommandHandler.Create(HandleCommand);
    }

    public Task<int> HandleCommand(string where, string dataSource, string rebaseTo)
    {
        return CliHelpers.RebaseSourceEntitiesAsync(
            new GetEntitiesWhereRequest { WhereClause = where, PageSize = 5000 },
            dataSource,
            rebaseTo,
            ServiceProvider);
    }
}
