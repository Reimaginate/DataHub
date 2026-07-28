using System.CommandLine.NamingConventionBinder;
using Reimaginate.CLI.Base.Attributes;
using Reimaginate.DataHub.CLI.Tools.Helpers;
using Reimaginate.DataHub.CLI.Tools.PluginBase;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Merge.Failures;

[Argument("where", required: false)]
public class MergeFailuresDeleteCommand : SubCommand<MergeFailuresCommand>
{
    public MergeFailuresDeleteCommand(IServiceProvider serviceProvider) : base("delete", "Delete merge failures", serviceProvider)
    {
        Handler = CommandHandler.Create(HandleCommand);
    }

    public async Task<int> HandleCommand(string? where = null)
    {
        return await CliHelpers.ProcessDeleteMergeFailures(true, ServiceProvider, where) ?? 1;
    }
}
