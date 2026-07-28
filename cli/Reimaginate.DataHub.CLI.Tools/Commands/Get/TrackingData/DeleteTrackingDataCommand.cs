using System.CommandLine.NamingConventionBinder;
using Reimaginate.CLI.Base.Attributes;
using Reimaginate.DataHub.CLI.Tools.Helpers;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using Reimaginate.DataHub.CLI.Tools.Shared.Helpers;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Get.TrackingData;

[Argument("dataSource", required: true)]
[Argument("entityType", required: true)]
[Argument("entityId", required: true)]
public class DeleteTrackingDataCommand : SubCommand<GetTrackingDataCommand>
{
    public DeleteTrackingDataCommand(IServiceProvider serviceProvider) : base("delete", "Delete tracking data", serviceProvider)
    {
        Handler = CommandHandler.Create(HandleCommand);
    }

    public async Task<int> HandleCommand(string dataSource, string entityType, string entityId)
        => await HandleCommandWithOptions(dataSource, entityType, entityId, dryRun: false, yes: false);

    public async Task<int> HandleCommandWithOptions(string dataSource, string entityType, string entityId, bool dryRun, bool yes)
    {
        var request = new GetTrackingDataRequest
        {
            DataSource = dataSource,
            EntityType = entityType,
            EntityIds = entityId == "*" ? null : new List<string> { entityId },
            PageSize = 1000
        };

        var resultsFunc = () => CliHelpers.RetrieveAllResultsAsync<GetTrackingDataRequest, GetTrackingDataResponse, ChangeTrackingEntry>(
            ServiceProvider,
            request,
            response => response.Results,
            response => response.MoreResultsAvailable,
            (req, res) => req.ContinuationToken = res.ContinuationToken);

        if (dryRun)
        {
            var entries = await resultsFunc();
            ConsoleHelper.PrintTable(entries, [
                nameof(ChangeTrackingEntry.id),
                nameof(ChangeTrackingEntry.DataSource),
                nameof(ChangeTrackingEntry.EntityType),
                nameof(ChangeTrackingEntry.EntityId)
            ]);
            return 1;
        }

        return await CliHelpers.ProcessDeleteTrackingData(true, resultsFunc, ServiceProvider, skipConfirmation: yes) ?? 1;
    }
}
