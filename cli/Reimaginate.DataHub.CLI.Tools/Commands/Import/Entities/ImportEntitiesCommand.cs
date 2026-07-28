using System.CommandLine.NamingConventionBinder;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using Reimaginate.CLI.Base.Attributes;
using Reimaginate.DataHub.CLI.Tools.Shared.Helpers;
using Reimaginate.DataHub.CLI.Tools.Shared.Requests.Send;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.Mediator;
using Spectre.Console;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Import.Entities;

[Option("conn", typeof(string), required: false, description: "Azure storage or Azure Cosmos connection string")]
[Option("container", typeof(string), required: false, description: "Azure storage or Azure Cosmos container name")]
[Option("continue", typeof(bool), required: false, description: "Continue on failure")]
[Option("notify-agents", typeof(bool), required: false, description: "Dispatch notifications to agents")]
[Option("overwrite", typeof(bool), required: false, description: "Overwrite/update existing records if found")]
[Option("path", typeof(string), required: false, description: "Path to local file or folder")]
[Option("pattern", typeof(string), required: false, description: "Regular expression pattern for file names to process")]
[Option("silent", typeof(bool), required: false, description: "Import without updating last updated timestamps")]
[Option("source", typeof(string), required: true, description: "AZStorage | LocalFile | LocalFolder | AZCosmos")]
[Option("untracked", typeof(bool), required: false, description: "Do not add tracking entries for imported records")]
[Option("data-only", typeof(bool), required: false, description: "Import files contain the raw record data instead of import requests")]
[Option("dry-run", typeof(bool), required: false, description: "Preview import input without writing entity data")]
[Option("yes", typeof(bool), required: false, description: "Confirm workload warnings without prompting")]

public class ImportEntitiesCommand : SubCommand<ImportCommand>
{
    private IMediator _mediator;
    public ImportEntitiesCommand(IServiceProvider serviceProvider) : base("entities", serviceProvider)
    {
        Handler = CommandHandler.Create(HandleCommand);
        _mediator = serviceProvider.GetRequiredService<IMediator>();
    }

    public async Task<int> HandleCommand(string source, string? path, string? conn, string? container, string? pattern, bool dataonly, bool silent, bool overwrite, bool untracked, bool notifyagents, bool @continue, bool dryrun = false, bool yes = false, CancellationToken cancellationToken = default)
    {
        switch (source.ToUpper())
        {
            case "AZSTORAGE":
                return await ImportFromAzStorage(conn, container, path, pattern, dataonly, silent, overwrite, untracked, notifyagents, @continue, cancellationToken);

            case "LOCALFILE":
                return await ImportFromLocalFile(path, dataonly, silent, overwrite, untracked, notifyagents, @continue, dryrun, yes, cancellationToken);

            case "LOCALFOLDER":
                return await ImportFromLocalFolder(path, pattern, dataonly, silent, overwrite, untracked, notifyagents, @continue, dryrun, yes, cancellationToken);

            case "AZCOSMOS":

                break;
        }

        return 1;
    }

    private async Task<int> ImportFromAzStorage(string? conn, string? container, string? path, string? pattern, bool dataonly, bool silent, bool overwrite, bool untracked, bool notifyagents, bool @continue, CancellationToken cancellationToken)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Markup("[lime]Importing entities from Azure Storage (press esc to cancel)[/]");
        AnsiConsole.WriteLine();

        var blobServiceClient = new BlobServiceClient(conn ?? string.Empty);
        var blobServiceContainer = blobServiceClient.GetBlobContainerClient(container ?? string.Empty);

        var prefix = $"{(path ?? string.Empty).TrimEnd('/')}/";

        Regex? regex = null;
        if (!string.IsNullOrEmpty(pattern))
        {
            regex = new Regex(pattern);
        }

        await foreach (var blobItem in blobServiceContainer.GetBlobsByHierarchyAsync(
                           traits: BlobTraits.None,
                           states: BlobStates.None,
                           delimiter: "/",
                           prefix: prefix,
                           cancellationToken: cancellationToken))
        {
            if (blobItem.IsBlob)
            {
                var fileName = blobItem.Blob.Name;
                if (regex != null && !regex.IsMatch(fileName)) continue;

                var blobClient = blobServiceContainer.GetBlobClient(blobItem.Blob.Name);
                var download = await blobClient.DownloadAsync(cancellationToken);

                using var reader = new StreamReader(download.Value.Content);
                var jsonContent = await reader.ReadToEndAsync(cancellationToken);
                var importItems = JArray.Parse(jsonContent);



            }
        }

        return 1;
    }

    private async Task<int> ImportFromLocalFile(string? path, bool dataonly, bool silent, bool overwrite, bool untracked, bool notifyagents, bool @continue, bool dryRun, bool yes, CancellationToken cancellationToken)
    {
        var filePath = path ?? string.Empty;
        AnsiConsole.WriteLine();
        AnsiConsole.Markup($"[lime]Importing records from {filePath} (press esc to cancel)[/]");
        AnsiConsole.WriteLine();

        if (!File.Exists(filePath))
        {
            AnsiConsole.Markup("[red]File not found[/]");
            return 0;
        }

        var fileContent = await File.ReadAllTextAsync(filePath, cancellationToken);
        var jsonContent = JArray.Parse(fileContent);

        var importRequests = dataonly ? jsonContent.Select(s => new ImportEntityRequest()
        {
            EntityType = s.Value<string>(nameof(DataHubEntity.entityType)),
            EntityId = s.Value<string>(nameof(DataHubEntity.id)),
            OverwriteIfExists = overwrite,
            Silent = silent,
            DispatchNotifications = notifyagents,
            Untracked = untracked,
            Data = (JObject)s
        }).ToList() : jsonContent.ToObject<List<ImportEntityRequest>>() ?? [];

        if (dryRun)
        {
            WriteImportPreview(importRequests, Path.GetFileName(filePath));
            return 1;
        }

        if ((!silent || notifyagents) && !yes)
        {
            if (!AnsiConsole.Confirm("[bold red]WARNING: NON-SILENT IMPORTING FROM LARGE FILES MAY CAUSE HIGH WORKLOADS ON THE DATAHUB. ARE YOU SURE YOU WANT TO CONTINUE[/]"))
            {
                AnsiConsole.WriteLine();
                return 1;
            }
        }

        return await ProcessImports(importRequests, silent, overwrite, untracked, notifyagents, @continue, Path.GetFileName(filePath), cancellationToken);
    }

    private async Task<int> ImportFromLocalFolder(string? path, string? pattern, bool dataonly, bool silent, bool overwrite, bool untracked, bool notifyagents, bool @continue, bool dryRun, bool yes, CancellationToken cancellationToken)
    {
        var folderPath = path ?? string.Empty;
        AnsiConsole.WriteLine();
        AnsiConsole.Markup($"[lime]Importing records from {folderPath} (press esc to cancel)[/]");
        AnsiConsole.WriteLine();

        if (!Directory.Exists(folderPath))
        {
            AnsiConsole.Markup("[red]Folder not found[/]");
            return 0;
        }

        if ((!silent || notifyagents) && !yes && !dryRun)
        {
            if (!AnsiConsole.Confirm("[bold red]WARNING: NON-SILENT IMPORTING FROM LARGE FILES OR FOLDERS MAY CAUSE HIGH WORKLOADS ON THE DATAHUB. ARE YOU SURE YOU WANT TO CONTINUE[/]"))
            {
                AnsiConsole.WriteLine();
                return 1;
            }
        }


        var filePaths = Directory.GetFiles(folderPath).ToList();
        if (!string.IsNullOrEmpty(pattern))
        {
            var regex = new Regex(pattern);
            filePaths = Directory.GetFiles(folderPath).Where(fileName => regex.IsMatch(Path.GetFileName(fileName))).ToList();
        }

        foreach (var file in filePaths)
        {
            var fileContent = await File.ReadAllTextAsync(file, cancellationToken);
            var jsonContent = JArray.Parse(fileContent);

            var importRequests = dataonly ? jsonContent.Select(s => new ImportEntityRequest()
            {
                EntityType = s.Value<string>(nameof(DataHubEntity.entityType)),
                EntityId = s.Value<string>(nameof(DataHubEntity.id)),
                OverwriteIfExists = overwrite,
                Silent = silent,
                DispatchNotifications = notifyagents,
                Untracked = untracked,
                Data = (JObject)s
            }).ToList() : jsonContent.ToObject<List<ImportEntityRequest>>() ?? [];

            if (dryRun)
            {
                WriteImportPreview(importRequests, Path.GetFileName(file));
                continue;
            }

            var result = await ProcessImports(importRequests, silent, overwrite, untracked, notifyagents, @continue, Path.GetFileName(file), cancellationToken);
            if (result != 1 && !@continue)
            {
                return result;
            }
        }

        return 1;
    }

    private static void WriteImportPreview(List<ImportEntityRequest> importRequests, string label)
    {
        ConsoleHelper.PrintTable([
            new ImportPreview
            {
                Source = label,
                Count = importRequests.Count,
                EntityTypes = string.Join(",", importRequests.Select(request => request.EntityType).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase))
            }
        ], [nameof(ImportPreview.Source), nameof(ImportPreview.Count), nameof(ImportPreview.EntityTypes)]);
    }

    private async Task<int> ProcessImports(List<ImportEntityRequest> importRequests, bool silent, bool overwrite, bool untracked, bool notifyagents, bool @continue, string label, CancellationToken cancellationToken)
    {
        try
        {
            var importRequestsToProcess = new List<ImportEntityRequest>(importRequests);
            var totalCount = importRequestsToProcess.Count;
            var importedCount = 0;
            var failures = new List<ImportEntityResponse>();

            var result = await CliProgressHelper
                .RunAsync(async ctx =>
                {
                    var consoleTask = ctx.AddTask($"Processing {label} : {totalCount} items");
                    consoleTask.StartTask();

                    while (importRequestsToProcess.Count != 0)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            AnsiConsole.Markup("[lime]Cancelling...");
                            break;
                        }

                        var batch = importRequestsToProcess.Take(1000).ToList();

                        var sendResponse = (await _mediator.TrySend<SendResponse>(new SendRequest(new ImportEntitiesRequest()
                        {
                            ImportEntityRequests = batch,
                            Silent = silent,
                            DispatchNotifications = notifyagents,
                            OverwriteIfExists = overwrite,
                            Untracked = untracked
                        }), cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var response } => response! };

                        var results = sendResponse.Result.ToObject<List<ImportEntityResponse>>() ?? [];

                        importedCount += results.Count(w => w.Success);
                        var batchFailures = results.Where(w => !w.Success).ToList();
                        if (batchFailures.Any())
                        {
                            failures.AddRange(batchFailures);
                            if (!@continue)
                            {
                                return 0;
                            }
                        }

                        var batchPercent = Convert.ToDouble(batch.Count) / Convert.ToDouble(totalCount) * 100;
                        consoleTask.Increment(batchPercent);

                        importRequestsToProcess.RemoveRange(0, batch.Count);
                    }

                    consoleTask.Value = 100;
                    consoleTask.StopTask();
                    return 1;
                });

            AnsiConsole.MarkupLine($"Imported [green]{importedCount}[/] of [yellow]{totalCount}[/] items from {Markup.Escape(label)}. Failed [red]{failures.Count}[/].");
            BulkOperationDetailsPrompt.Show(
                failures,
                [
                    nameof(ImportEntityResponse.EntityType),
                    nameof(ImportEntityResponse.EntityId),
                    nameof(ImportEntityResponse.FailureReason)
                ],
                "entities-import-errors",
                "failed import result(s)");

            return result;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return 0;
        }
    }

    private sealed class ImportPreview
    {
        public string Source { get; set; } = string.Empty;
        public int Count { get; set; }
        public string EntityTypes { get; set; } = string.Empty;
    }
}
