using System.CommandLine.NamingConventionBinder;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Reimaginate.CLI.Base.Attributes;
using Reimaginate.DataHub.CLI.Tools.Helpers;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.CLI.Tools.Shared.Helpers;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Core.Models.Duplicates;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Spectre.Console;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Get.Duplicates;

[Argument("id", required: true)]
[Option("properties", required: false, aliases: "props,p")]
[Option("additional-properties", required: false, aliases: "add-props")]
public class GetDuplicateByIdCommand : SubCommand<GetDuplicatesCommand>
{
    public GetDuplicateByIdCommand(IServiceProvider serviceProvider) : base("byid", "Get a duplicate by id", serviceProvider)
    {
        Handler = CommandHandler.Create(HandleCommand);
    }

    private readonly string _defaultProperties = string.Join(",", new[]
    {
        nameof(Duplicate.id),
        nameof(Duplicate.Name),
        nameof(Duplicate.EntityType),
        nameof(Duplicate.Status),
        nameof(Duplicate.FailureReason)
    });

    public async Task<int> HandleCommand(string id, string? properties = null, string? additionalProperties = null, CancellationToken cancellationToken = default)
    {
        var props = CliHelpers.ParseDisplayProps(_defaultProperties, properties, additionalProperties);
        var adminApi = ServiceProvider.GetRequiredService<ICLIApi>();
        var response = await adminApi.PostAdminMessage<GetDuplicateResponse>(new SerializedRequest
        {
            RequestType = nameof(GetDuplicateRequest),
            Data = JsonConvert.SerializeObject(new GetDuplicateRequest { Id = id })
        }, cancellationToken);

        ConsoleHelper.PrintTable(new[] { response.Result }.Where(duplicate => duplicate != null).ToList(), props);
        AnsiConsole.WriteLine();
        return 1;
    }
}
