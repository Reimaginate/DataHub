using Reimaginate.DataHub.CLI.Tools.PluginBase;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Sync.Failures;

public class SyncFailuresCommand(IServiceProvider serviceProvider) : SubCommand<SyncCommand>("failures", "Repair sync failures", serviceProvider);
