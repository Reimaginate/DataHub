using System.CommandLine.NamingConventionBinder;
using System.Text.RegularExpressions;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using Reimaginate.CLI.Base.Attributes;
using Reimaginate.DataHub.CLI.Tools.Shared.Helpers;
using Reimaginate.DataHub.CLI.Tools.Shared.Requests.Send;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.Mediator;
using Spectre.Console;
using PatchEntitiesRequest = Reimaginate.DataHub.SharedModels.Requests.CLI.PatchEntitiesRequest;
using PatchEntityRequest = Reimaginate.DataHub.SharedModels.Requests.CLI.PatchEntityRequest;
using PatchEntityResponse = Reimaginate.DataHub.SharedModels.Requests.CLI.PatchEntityResponse;


namespace Reimaginate.DataHub.CLI.Tools.Commands.Patch.Entities;

[Option("source", typeof(string), required: true, description: "AZStorage | LocalFile | LocalFolder | AZCosmos")]
[Option("path", typeof(string), required: false, description: "Path to local file or folder")]
[Option("where", typeof(string), required: false, description: "DataHub query selecting entities to patch")]
[Option("pattern", typeof(string), required: false, description: "Regular expression pattern for file names to process")]
[Option("silent", typeof(bool), required: false, description: "Patch without updating last updated timestamps")]
[Option("notify-agents", typeof(bool), required: false, description: "Dispatch notifications to agents")]
[Option("continue", typeof(bool), required: false, description: "Continue on failure")]
[Option("conn", typeof(string), required: false, description: "Azure storage or Azure Cosmos connection string")]
[Option("container", typeof(string), required: false, description: "Azure storage or Azure Cosmos container name")]
[Option("dry-run", typeof(bool), required: false, description: "Preview patch input and matched entities without applying patches")]
[Option("yes", typeof(bool), required: false, description: "Confirm patch warnings without prompting")]
public class PatchEntitiesCommand : SubCommand<PatchCommand>
{
    private IMediator _mediator;
    public PatchEntitiesCommand(IServiceProvider serviceProvider) : base("entities", serviceProvider)
    {
        Handler = CommandHandler.Create(HandleCommand);
        _mediator = serviceProvider.GetRequiredService<IMediator>();
    }

    public async Task<int> HandleCommand(string source, string? path, string? where, string? conn, string? container, string? pattern, bool silent, bool notifyagents, bool @continue, bool dryrun = false, bool yes = false, CancellationToken cancellationToken = default)
    {
        switch (source.ToUpper())
        {
            case "AZSTORAGE":
                return await PatchFromAzStorage(conn, container, path, pattern, silent, notifyagents, @continue, cancellationToken);

            case "LOCALFILE":
                return await PatchFromLocalFile(path, silent, notifyagents, @continue, dryrun, yes, cancellationToken);

            case "LOCALFOLDER":
                return await PatchFromLocalFolder(path, pattern, silent, notifyagents, @continue, dryrun, yes, cancellationToken);

            case "DATAHUB":
                return await PatchFromDataHub(where, path, silent, notifyagents, @continue, dryrun, yes, cancellationToken);
        }

        return 1;
    }

    private async Task<int> PatchFromAzStorage(string? conn, string? container, string? path, string? pattern, bool silent, bool notifyagents, bool @continue, CancellationToken cancellationToken)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Markup("[lime]Importing patches from Azure Storage (press esc to cancel)[/]");
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
                var patches = JArray.Parse(jsonContent);



            }
        }

        return 1;
    }

    private async Task<int> PatchFromLocalFile(string? path, bool silent, bool notifyagents, bool @continue, bool dryRun, bool yes, CancellationToken cancellationToken)
    {
        var filePath = path ?? string.Empty;
        AnsiConsole.WriteLine();
        AnsiConsole.Markup($"[lime]Importing patches from {filePath} (press esc to cancel)[/]");
        AnsiConsole.WriteLine();

        if (!File.Exists(filePath))
        {
            AnsiConsole.Markup("[red]File not found[/]");
            return 0;
        }

        var fileContent = await File.ReadAllTextAsync(filePath, cancellationToken);
        var jsonContent = JArray.Parse(fileContent);
        var patchRequests = jsonContent.ToObject<List<PatchEntityRequest>>() ?? [];

        if (dryRun)
        {
            WritePatchPreview(Path.GetFileName(filePath), patchRequests.Count, patchRequests.Select(request => request.EntityType));
            return 1;
        }

        if ((!silent || notifyagents) && !yes)
        {
            if (!AnsiConsole.Confirm("[bold red]WARNING: NON-SILENT PATCHING FROM LARGE FILES MAY CAUSE HIGH WORKLOADS ON THE DATAHUB. ARE YOU SURE YOU WANT TO CONTINUE[/]"))
            {
                AnsiConsole.WriteLine();
                return 1;
            }
        }

        return await ProcessPatches(patchRequests, silent, notifyagents, @continue, Path.GetFileName(filePath), cancellationToken);
    }

    private async Task<int> PatchFromLocalFolder(string? path, string? pattern, bool silent, bool notifyagents, bool @continue, bool dryRun, bool yes, CancellationToken cancellationToken)
    {
        var folderPath = path ?? string.Empty;
        AnsiConsole.WriteLine();
        AnsiConsole.Markup($"[lime]Importing patches from {folderPath} (press esc to cancel)[/]");
        AnsiConsole.WriteLine();

        if (!Directory.Exists(folderPath))
        {
            AnsiConsole.Markup("[red]Folder not found[/]");
            return 0;
        }

        if ((!silent || notifyagents) && !yes && !dryRun)
        {
            if (!AnsiConsole.Confirm("[bold red]WARNING: NON-SILENT PATCHING FROM LARGE FILES OR FOLDERS MAY CAUSE HIGH WORKLOADS ON THE DATAHUB. ARE YOU SURE YOU WANT TO CONTINUE[/]"))
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
            var patchRequests = jsonContent.ToObject<List<PatchEntityRequest>>() ?? [];
            if (dryRun)
            {
                WritePatchPreview(Path.GetFileName(file), patchRequests.Count, patchRequests.Select(request => request.EntityType));
                continue;
            }

            var result = await ProcessPatches(patchRequests, silent, notifyagents, @continue, Path.GetFileName(file), cancellationToken);
            if (result != 1 && !@continue)
            {
                return result;
            }
        }

        return 1;
    }


    private async Task<int> PatchFromDataHub(string? where, string? path, bool silent, bool notifyagents, bool @continue, bool dryRun, bool yes, CancellationToken cancellationToken)
    {
        var query = where ?? string.Empty;
        var filePath = path ?? string.Empty;
        AnsiConsole.WriteLine();
        AnsiConsole.Markup($"[lime]Importing patches from DataHub using query {query} (press esc to cancel)[/]");
        AnsiConsole.WriteLine();

        var fileContent = await File.ReadAllTextAsync(filePath, cancellationToken);
        var trimmedFileContent = fileContent.Trim();
        if (!(trimmedFileContent.StartsWith('[') && trimmedFileContent.EndsWith(']')))
        {
            fileContent = $"[{fileContent}]";
        }
        var jsonContent = JArray.Parse(fileContent);
        var patches = jsonContent.ToObject<List<SharedModels.Core.Patch>>() ?? [];

        var getEntitiesRequest = new GetEntitiesWhereRequest()
        {
            WhereClause = query,
            GetTotalResultCount = true,
            PageSize = 1000
        };

        var sendResponse = (await _mediator.TrySend<SendResponse>(new SendRequest(getEntitiesRequest), cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var response } => response! };
        var getEntitiesResponse = sendResponse.Result.ToObject<GetEntitiesResponse>()!;

        if (!getEntitiesResponse.Results.Any())
        {
            AnsiConsole.Markup("[yellow]\tNo matching entities found[/]");
            return 0;
        }

        if (dryRun)
        {
            WritePatchPreview(filePath, getEntitiesResponse.ResultCount, getEntitiesResponse.Results.Select(result => result.Value<string>(nameof(DataHubEntity.entityType))));
            return 1;
        }
        
        if (!yes && !AnsiConsole.Confirm($"[bold red]WARNING: THIS OPERATION WILL UPDATE {getEntitiesResponse.ResultCount} DATAHUB RECORDS. ARE YOU SURE YOU WANT TO CONTINUE[/]"))
        {
            AnsiConsole.WriteLine();
            return 1;
        }

        if ((!silent || notifyagents) && !yes)
        {
            if (!AnsiConsole.Confirm("[bold red]WARNING: NON-SILENT PATCHING OF LARGE RECORD SETS MAY CAUSE HIGH WORKLOADS ON THE DATAHUB. ARE YOU SURE YOU WANT TO CONTINUE[/]"))
            {
                AnsiConsole.WriteLine();
                return 1;
            }
        }

        var failures = new List<PatchEntityResponse>();
        var matchedCount = getEntitiesResponse.ResultCount;
        await CliProgressHelper
            .RunAsync(async ctx =>
            {
                var totalCount = matchedCount;
                var consoleTask = ctx.AddTask($"Processing {totalCount} items");
                consoleTask.StartTask();

                var groupedByEntityType = getEntitiesResponse.Results.GroupBy(g => g.Value<string>(nameof(DataHubEntity.entityType)));
                foreach (var group in groupedByEntityType)
                {
                    var batch = group.ToList();
                    var patchEntityRequests = batch.Select(s => new PatchEntityRequest()
                    {
                        DataSource = DataSources.DataHub,
                        EntityType = group.Key,
                        EntityId = s.Value<string>(nameof(DataHubEntity.id)),
                        Operations = patches
                    }).ToList();

                    sendResponse = (await _mediator.TrySend<SendResponse>(new SendRequest(new PatchEntitiesRequest()
                    {
                        Requests = patchEntityRequests,
                        Silent = silent,
                        DispatchNotifications = notifyagents
                    }), cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var response } => response! };

                    var results = sendResponse.Result.ToObject<List<PatchEntityResponse>>();

                    var groupFailures = (results ?? []).Where(w => !w.Success).ToList();
                    if (groupFailures.Any())
                    {
                        failures.AddRange(groupFailures);

                        if (!@continue)
                        {
                            return 0;
                        }
                    }

                    var batchPercent = Convert.ToDouble(batch.Count) / Convert.ToDouble(totalCount) * 100;
                    consoleTask.Increment(batchPercent);
                }

                while (getEntitiesResponse.MoreResultsAvailable)
                {
                    getEntitiesRequest.GetTotalResultCount = false;
                    getEntitiesRequest.ContinuationToken = getEntitiesResponse.ContinuationToken;

                    sendResponse = (await _mediator.TrySend<SendResponse>(new SendRequest(getEntitiesRequest), cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var response } => response! };
                    getEntitiesResponse = sendResponse.Result.ToObject<GetEntitiesResponse>()!;

                    groupedByEntityType = getEntitiesResponse.Results.GroupBy(g => g.Value<string>(nameof(DataHubEntity.entityType)));
                    foreach (var group in groupedByEntityType)
                    {
                        var batch = group.ToList();
                        var patchEntityRequests = batch.Select(s => new PatchEntityRequest()
                        {
                            DataSource = DataSources.DataHub,
                            EntityType = group.Key,
                            EntityId = s.Value<string>(nameof(DataHubEntity.id)),
                            Operations = patches
                        }).ToList();

                        sendResponse = (await _mediator.TrySend<SendResponse>(new SendRequest(new PatchEntitiesRequest()
                        {
                            Requests = patchEntityRequests,
                            Silent = silent,
                            DispatchNotifications = notifyagents
                        }), cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var response } => response! };

                        var results = sendResponse.Result.ToObject<List<PatchEntityResponse>>();

                        var groupFailures = (results ?? []).Where(w => !w.Success).ToList();
                        if (groupFailures.Any())
                        {
                            failures.AddRange(groupFailures);

                            if (!@continue)
                            {
                                return 0;
                            }
                        }

                        var batchPercent = Convert.ToDouble(batch.Count) / Convert.ToDouble(totalCount) * 100;
                        consoleTask.Increment(batchPercent);
                    }

                }

                return failures.Any() && !@continue ? 0 : 1;
            });

        AnsiConsole.WriteLine($"Patched {Math.Max(matchedCount - failures.Count, 0)} of {matchedCount} DataHub entities. Failed {failures.Count}.");
        ShowPatchFailures(failures, "entities-patch-errors");
        return 1;
    }

    private static void WritePatchPreview(string source, int count, IEnumerable<string?> entityTypes)
    {
        ConsoleHelper.PrintTable([
            new PatchPreview
            {
                Source = source,
                Count = count,
                EntityTypes = string.Join(",", entityTypes.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase))
            }
        ], [nameof(PatchPreview.Source), nameof(PatchPreview.Count), nameof(PatchPreview.EntityTypes)]);
    }

    private async Task<int> ProcessPatches(List<PatchEntityRequest> patches, bool silent, bool notifyagents, bool @continue, string label, CancellationToken cancellationToken)
    {
        try
        {
            var patchesToProcess = new List<PatchEntityRequest>(patches);
            var failures = new List<PatchEntityResponse>();
            var totalCount = patchesToProcess.Count;

            var result = await CliProgressHelper
                .RunAsync(async ctx =>
                {
                    var consoleTask = ctx.AddTask($"Processing {label} : {totalCount} items");
                    consoleTask.StartTask();

                    while (patchesToProcess.Count != 0)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            AnsiConsole.Markup($"[lime]Cancelling...");
                            break;
                        }

                        var batch = patchesToProcess.Take(1000).ToList();

                        var sendResponse = (await _mediator.TrySend<SendResponse>(new SendRequest(new PatchEntitiesRequest()
                        {
                            Requests = batch,
                            Silent = silent,
                            DispatchNotifications = notifyagents
                        }), cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var response } => response! };

                        var results = sendResponse.Result.ToObject<List<PatchEntityResponse>>() ?? [];

                        var groupFailures = results.Where(w => !w.Success).ToList();
                        if (groupFailures.Any())
                        {
                            failures.AddRange(groupFailures);

                            if (!@continue)
                            {
                                return 0;
                            }
                        }

                        var batchPercent = Convert.ToDouble(batch.Count) / Convert.ToDouble(totalCount) * 100;
                        consoleTask.Increment(batchPercent);

                        patchesToProcess.RemoveRange(0, batch.Count);
                    }

                    consoleTask.Value = 100;
                    consoleTask.StopTask();
                    return failures.Any() && !@continue ? 0 : 1;
                });

            AnsiConsole.WriteLine($"Patched {Math.Max(totalCount - failures.Count, 0)} of {totalCount} items from {label}. Failed {failures.Count}.");
            ShowPatchFailures(failures, "entities-patch-errors");
            return result;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return 0;
        }
    }

    private static void ShowPatchFailures(List<PatchEntityResponse> failures, string filePrefix)
    {
        BulkOperationDetailsPrompt.Show(
            ToPatchFailureDetails(failures),
            [
                nameof(PatchFailureDetail.DataSource),
                nameof(PatchFailureDetail.EntityType),
                nameof(PatchFailureDetail.EntityId),
                nameof(PatchFailureDetail.FailureReason)
            ],
            filePrefix,
            "failed entity patch result(s)");
    }

    private static List<PatchFailureDetail> ToPatchFailureDetails(IEnumerable<PatchEntityResponse> failures)
    {
        var details = new List<PatchFailureDetail>();
        foreach (var failure in failures)
        {
            if (failure.PatchFailures?.Count > 0)
            {
                details.AddRange(failure.PatchFailures.Select(patchFailure => new PatchFailureDetail
                {
                    DataSource = failure.DataSource,
                    EntityType = failure.EntityType,
                    EntityId = failure.EntityId,
                    FailureReason = patchFailure.FailureReason,
                    Patch = patchFailure.Patch
                }));
                continue;
            }

            details.Add(new PatchFailureDetail
            {
                DataSource = failure.DataSource,
                EntityType = failure.EntityType,
                EntityId = failure.EntityId,
                FailureReason = failure.FailureReason
            });
        }

        return details;
    }

    private sealed class PatchFailureDetail
    {
        public string DataSource { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string FailureReason { get; set; } = string.Empty;
        public Reimaginate.DataHub.SharedModels.Core.Patch? Patch { get; set; }
    }

    private sealed class PatchPreview
    {
        public string Source { get; set; } = string.Empty;
        public int Count { get; set; }
        public string EntityTypes { get; set; } = string.Empty;
    }
}
