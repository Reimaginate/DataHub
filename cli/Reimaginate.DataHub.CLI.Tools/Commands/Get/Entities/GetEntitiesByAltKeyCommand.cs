using System.CommandLine.NamingConventionBinder;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Reimaginate.CLI.Base.Attributes;
using Reimaginate.DataHub.CLI.Tools.Helpers;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.CLI.Tools.Shared.Helpers;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Spectre.Console;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Get.Entities;

[Option("key", typeof(string), required: true, description: "Alternate key name, usually <dataSource>.<sourceEntityType>.")]
[Option("value", typeof(string), required: true, description: "Alternate key value.")]
[Option("properties", required: false, aliases: "props,p")]
[Option("additional-properties", required: false, aliases: "add-props")]
public class GetEntitiesByAltKeyCommand : SubCommand<GetEntitiesCommand>
{
    public GetEntitiesByAltKeyCommand(IServiceProvider serviceProvider) : base("find-by-alt-key", "Find entities by alternate key", serviceProvider)
    {
        Handler = CommandHandler.Create(HandleCommand);
    }

    private readonly string _defaultProperties = string.Join(",", new[]
    {
        nameof(DataHubEntity.entityType),
        nameof(DataHubEntity.id),
        nameof(DataHubEntity.alternateKeys)
    });

    public async Task<int> HandleCommand(string key, string value, string? properties = null, string? additionalProperties = null, CancellationToken cancellationToken = default)
    {
        var adminApi = ServiceProvider.GetRequiredService<ICLIApi>();
        var response = await adminApi.PostAdminMessage<GetEntitiesResponse>(new SerializedRequest
        {
            RequestType = nameof(GetEntitiesByAltKeyRequest),
            Data = JsonConvert.SerializeObject(new GetEntitiesByAltKeyRequest
            {
                AlternateKeys = [new AlternateKey(key, value)]
            })
        }, cancellationToken);

        var props = CliHelpers.ParseDisplayProps(_defaultProperties, properties, additionalProperties);
        ConsoleHelper.PrintTable(CliHelpers.SummarizeResults(false, response.Results), props);
        AnsiConsole.WriteLine();
        return 1;
    }
}
