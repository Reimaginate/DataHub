using System.CommandLine.NamingConventionBinder;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using Reimaginate.CLI.Base.Attributes;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Spectre.Console;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Export.Entities;

[Argument("entityType", required: true)]
[Argument("entityIds", allowMultiple: true, required: true, type: typeof(string[]))]
[Option("save-to", required: false, aliases: "s")]
public class ExportEntitiesByIdCommand : SubCommand<ExportEntitiesCommand>
{
    public ExportEntitiesByIdCommand(IServiceProvider serviceProvider) : base("byid", serviceProvider)
    {
        Handler = CommandHandler.Create(HandleCommand);
    }

    public async Task<int> HandleCommand(string entityType, string[] entityIds, string? saveTo = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var adminApi = ServiceProvider.GetRequiredService<ICLIApi>();
            var (fileType, filePath) = ExportHelpers.DetermineFileTypeAndPath(saveTo);

            ExportHelpers.EnsureDirectoryExists(filePath, fileType == ExportHelpers.ExportFileType.Folder);

            switch (fileType)
            {
                case ExportHelpers.ExportFileType.Zip:
                    await ExportHelpers.ExportToZipAsync(adminApi, entityType, entityIds.ToList(), filePath, cancellationToken);
                    break;

                case ExportHelpers.ExportFileType.Json:
                    await ExportHelpers.ExportToSingleJsonAsync(adminApi, entityType, entityIds.ToList(), filePath, cancellationToken);
                    break;

                case ExportHelpers.ExportFileType.Folder:
                    await ExportHelpers.ExportToFolderAsync(adminApi, entityType, entityIds.ToList(), filePath, cancellationToken);
                    break;
                default:
                    AnsiConsole.MarkupLineInterpolated($"[red]Unsupported file type: {fileType}[/]");
                    return 1;
            }

            AnsiConsole.MarkupLineInterpolated($"[green]Export completed successfully to {filePath}[/]");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Error during export: {ex.Message}[/]");
            return 1;
        }
    }
}
