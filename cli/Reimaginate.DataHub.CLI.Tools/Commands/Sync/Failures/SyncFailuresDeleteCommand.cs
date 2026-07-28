using System.CommandLine.NamingConventionBinder;
using Reimaginate.CLI.Base.Attributes;
using Reimaginate.DataHub.CLI.Tools.Helpers;
using Reimaginate.DataHub.CLI.Tools.PluginBase;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Sync.Failures;

[Argument("where", required: false)]
public class SyncFailuresDeleteCommand : SubCommand<SyncFailuresCommand>
{
    public SyncFailuresDeleteCommand(IServiceProvider serviceProvider) : base("delete", "Delete sync failures", serviceProvider)
    {
        Handler = CommandHandler.Create(HandleCommand);
    }

    public async Task<int> HandleCommand(string? where = null)
    {
        return await CliHelpers.ProcessDeleteSyncFailures(true, ServiceProvider, where) ?? 1;
    }
}
