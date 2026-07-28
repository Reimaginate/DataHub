using Reimaginate.DataHub.CLI.Tools.PluginBase;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Import;

public class ImportCommand(IServiceProvider serviceProvider) : TopLevelCommand("import", serviceProvider);