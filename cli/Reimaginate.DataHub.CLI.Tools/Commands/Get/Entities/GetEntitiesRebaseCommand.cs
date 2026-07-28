using System.CommandLine.NamingConventionBinder;
using Reimaginate.CLI.Base.Attributes;
using Reimaginate.DataHub.CLI.Tools.Helpers;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Get.Entities;

[Argument("where", required: true)]
[Argument("rebaseTo", required: true)]
public class GetEntitiesRebaseCommand : SubCommand<GetEntitiesCommand>
{
    public GetEntitiesRebaseCommand(IServiceProvider serviceProvider) : base("rebase", "Rebase DataHub entities", serviceProvider)
    {
        Handler = CommandHandler.Create(HandleCommand);
    }

    public Task<int> HandleCommand(string where, string rebaseTo)
    {
        return CliHelpers.RebaseEntitiesAsync(
            new GetEntitiesWhereRequest { WhereClause = where, PageSize = 5000 },
            rebaseTo,
            ServiceProvider);
    }
}
