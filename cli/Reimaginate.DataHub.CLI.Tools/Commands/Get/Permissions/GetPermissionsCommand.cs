using System.CommandLine.NamingConventionBinder;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Reimaginate.CLI.Base.Attributes;
using Reimaginate.DataHub.CLI.Tools.Helpers;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.CLI.Tools.Shared.Helpers;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Spectre.Console;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Get.Permissions;

[Option("properties", required: false, aliases: "props,p")]
[Option("additional-properties", required: false, aliases: "add-props")]
public class GetPermissionsCommand : SubCommand<GetCommand>
{
    private readonly string _defaultProperties = string.Join(",", new List<string>
    {
        nameof(DataHubPermissionDTO.Name)
    });

    public GetPermissionsCommand(IServiceProvider serviceProvider) : base("permissions", serviceProvider)
    {
        Handler = CommandHandler.Create(HandleCommand);
    }

    public async Task<int> HandleCommand(string? properties = null, string? additionalProperties = null, CancellationToken cancellationToken = default)
    {
        var adminApi = ServiceProvider.GetRequiredService<ICLIApi>();

        var response = await adminApi.PostAdminMessage<GetPermissionsResponse>(new SerializedRequest
        {
            RequestType = nameof(GetPermissionsRequest),
            Data = JsonConvert.SerializeObject(new GetPermissionsRequest())
        }, cancellationToken);

        if (!response.Success)
        {
            AnsiConsole.WriteLine("Get permissions failed: " + response.FailureReason);
            return 0;
        }

        if (response.Results == null || !response.Results.Any())
        {
            AnsiConsole.WriteLine("No results");
            return 1;
        }

        var results = CliHelpers.SummarizeResults(true, response.Results);
        var props = CliHelpers.ParseDisplayProps(_defaultProperties, properties, additionalProperties);
        ConsoleHelper.PrintTable(results, props);
        AnsiConsole.WriteLine();

        return 1;
    }
}
