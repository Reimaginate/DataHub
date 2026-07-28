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

namespace Reimaginate.DataHub.CLI.Tools.Commands.Get.Roles;

[Option("properties", required: false, aliases: "props,p")]
[Option("additional-properties", required: false, aliases: "add-props")]
public class GetRolesCommand : SubCommand<GetCommand>
{
    private readonly string _defaultProperties = string.Join(",", new List<string>
    {
        nameof(DataHubRoleDTO.Name),
        nameof(DataHubRoleDTO.Description),
        nameof(DataHubRoleDTO.BuiltIn),
        nameof(DataHubRoleDTO.Permissions)
    });

    public GetRolesCommand(IServiceProvider serviceProvider) : base("roles", serviceProvider)
    {
        Handler = CommandHandler.Create(HandleCommand);
    }

    public async Task<int> HandleCommand(string? properties = null, string? additionalProperties = null, CancellationToken cancellationToken = default)
    {
        var adminApi = ServiceProvider.GetRequiredService<ICLIApi>();

        var response = await adminApi.PostAdminMessage<GetRolesResponse>(new SerializedRequest
        {
            RequestType = nameof(GetRolesRequest),
            Data = JsonConvert.SerializeObject(new GetRolesRequest())
        }, cancellationToken);

        return PrintResponse(response, properties, additionalProperties);
    }

    public int PrintResponse(GetRolesResponse response, string? properties = null, string? additionalProperties = null)
    {
        if (!response.Success)
        {
            AnsiConsole.WriteLine("Get roles failed: " + response.FailureReason);
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
