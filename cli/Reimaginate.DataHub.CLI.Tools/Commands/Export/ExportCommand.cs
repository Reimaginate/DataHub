using Reimaginate.DataHub.CLI.Tools.PluginBase;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Export;

public class ExportCommand(IServiceProvider serviceProvider) : TopLevelCommand("export", serviceProvider);
// datahub export entities byid --save-to
// datahub export entities where --save-to