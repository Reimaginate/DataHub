using Reimaginate.DataHub.CLI.Tools.PluginBase;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Export.Entities;
public class ExportEntitiesCommand(IServiceProvider serviceProvider) : SubCommand<ExportCommand>("entities", serviceProvider);