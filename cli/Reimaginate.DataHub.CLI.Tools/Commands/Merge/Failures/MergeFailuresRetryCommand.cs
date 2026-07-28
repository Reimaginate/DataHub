using System.CommandLine.NamingConventionBinder;
using Reimaginate.CLI.Base.Attributes;
using Reimaginate.DataHub.CLI.Tools.Helpers;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Merge.Failures;

[Argument("where", required: false)]
public class MergeFailuresRetryCommand : SubCommand<MergeFailuresCommand>
{
    public MergeFailuresRetryCommand(IServiceProvider serviceProvider) : base("retry", "Retry merge failures", serviceProvider)
    {
        Handler = CommandHandler.Create(HandleCommand);
    }

    public async Task<int> HandleCommand(string? where = null)
    {
        var request = new GetMergeFailuresWhereRequest { WhereClause = where, PageSize = 1000 };
        var resultsFunc = () => CliHelpers.RetrieveAllResultsAsync<GetMergeFailuresWhereRequest, GetMergeFailuresResponse, MergeFailureDTO>(
            ServiceProvider,
            request,
            response => response.Results,
            response => response.MoreResultsAvailable,
            (req, res) => req.ContinuationToken = res.ContinuationToken);

        return await CliHelpers.ProcessRetryMergeFailures(true, ServiceProvider, resultsFunc) ?? 1;
    }
}
