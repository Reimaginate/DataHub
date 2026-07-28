using Reimaginate.DataHub.CLI.Tools.PluginBase;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Merge.Failures;

public class MergeFailuresCommand(IServiceProvider serviceProvider) : SubCommand<MergeCommand>("failures", "Repair merge failures", serviceProvider);
