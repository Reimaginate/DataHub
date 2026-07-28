using Reimaginate.DataHub.CLI.Tools.PluginBase;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Patch.Failures;

public class PatchFailuresCommand(IServiceProvider serviceProvider) : SubCommand<PatchCommand>("failures", "Repair patch failures", serviceProvider);
