using Reimaginate.DataHub.CLI.Tools.PluginBase;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Delete.Jobs;

public class DeleteJobsCommand(IServiceProvider serviceProvider) : SubCommand<DeleteCommand>("jobs", serviceProvider);

