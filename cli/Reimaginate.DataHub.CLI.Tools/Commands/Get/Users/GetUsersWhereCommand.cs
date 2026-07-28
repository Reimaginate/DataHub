using System.CommandLine.NamingConventionBinder;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Reimaginate.DataHub.CLI.Tools.Helpers;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using Reimaginate.CLI.Base.Attributes;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.CLI.Tools.Shared.Helpers;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;

using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Spectre.Console;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Get.Users;

[Argument("where", required: true)]
[Option("properties", required: false, aliases: "props,p")]
[Option("additional-properties", required: false, aliases: "add-props")]
public class GetUsersWhereCommand : SubCommand<GetUsersCommand>
{
    public GetUsersWhereCommand(IServiceProvider serviceProvider) : base("where", serviceProvider)
    {
        Handler = CommandHandler.Create(HandleCommand);
    }

    private readonly string _defaultProperties = string.Join(",", new List<string>()
    {
        nameof(UserDTO.Id),
        nameof(UserDTO.Name),
        nameof(UserDTO.Email),
        nameof(UserDTO.TenantId),
        nameof(UserDTO.UPN),
        nameof(UserDTO.Disabled)
    });

    public async Task<int> HandleCommand(string where, string? properties = null, string? additionalProperties = null, CancellationToken cancellationToken = default)
    {
        var adminApi = ServiceProvider.GetRequiredService<ICLIApi>();

        var response = await adminApi.PostAdminMessage<GetUsersResponse>(new SerializedRequest()
        {
            RequestType = nameof(GetUsersRequest),
            Data = JsonConvert.SerializeObject(new GetUsersRequest()
            {
                Where = where
            })
        }, cancellationToken);

        var users = response.Results;

        if (users == null || !users.Any())
        {
            AnsiConsole.WriteLine("No results");
            return 1;
        }

        var results = CliHelpers.SummarizeResults(true, users);

        var props = CliHelpers.ParseDisplayProps(_defaultProperties, properties, additionalProperties);
        ConsoleHelper.PrintTable(results, props);
        AnsiConsole.WriteLine();

        return 1;
    }
}

