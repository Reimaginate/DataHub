using System.CommandLine.NamingConventionBinder;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using Reimaginate.CLI.Base.Attributes;
using Reimaginate.DataHub.CLI.Tools.Shared.Helpers;
using Reimaginate.DataHub.CLI.Tools.Shared.Requests.Send;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.Mediator;
using Spectre.Console;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Register.AlternateKeys;

[Option("source", typeof(string), required: true, description: "AZStorage | LocalFile | LocalFolder | AZCosmos")]
[Option("path", typeof(string), required: false, description: "Path to local file or folder")]
[Option("pattern", typeof(string), required: false, description: "Regular expression pattern for file names to process")]
[Option("silent", typeof(bool), required: false, description: "Register alternate keys without updating last updated timestamps")]
[Option("continue", typeof(bool), required: false, description: "Continue on failure")]
[Option("conn", typeof(string), required: false, description: "Azure storage or Azure Cosmos connection string")]
[Option("container", typeof(string), required: false, description: "Azure storage or Azure Cosmos container name")]
public class RegisterAlternateKeysCommand : SubCommand<RegisterCommand>
{

    private IMediator _mediator;

    public RegisterAlternateKeysCommand(IServiceProvider serviceProvider) : base("alternatekeys", serviceProvider)
    {
        Handler = CommandHandler.Create(HandleCommand);
        _mediator = serviceProvider.GetRequiredService<IMediator>();
    }

    public async Task<int> HandleCommand(string source, string? path, string? conn, string? container, string? pattern, bool silent, bool @continue, CancellationToken cancellationToken = default)
    {
        switch (source.ToUpper())
        {
            case "AZSTORAGE":
                return await RegisterAlternateKeysFromAzStorage(conn, container, path, pattern, silent, @continue, cancellationToken);

            case "LOCALFILE":
                return await RegisterAlternateKeysFromLocalFile(path, silent, @continue, cancellationToken);

            case "LOCALFOLDER":
                return await RegisterAlternateKeysFromLocalFolder(path, pattern, silent, @continue, cancellationToken);

            case "AZCOSMOS":

                break;
        }

        return 1;
    }

    private async Task<int> RegisterAlternateKeysFromAzStorage(string? conn, string? container, string? path, string? pattern, bool silent, bool @continue, CancellationToken cancellationToken)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Markup("[lime]Importing alternate keys from Azure Storage (press esc to cancel)[/]");
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
                var alternateKeys = JArray.Parse(jsonContent);
            }
        }

        return 1;
    }

    private async Task<int> RegisterAlternateKeysFromLocalFile(string? path, bool silent, bool @continue, CancellationToken cancellationToken)
    {
        var filePath = path ?? string.Empty;
        AnsiConsole.WriteLine();
        AnsiConsole.Markup($"[lime]Importing alternate keys from {filePath} (press esc to cancel)[/]");
        AnsiConsole.WriteLine();

        if (!File.Exists(filePath))
        {
            AnsiConsole.Markup("[red]File not found[/]");
            return 0;
        }

        var fileContent = await File.ReadAllTextAsync(filePath, cancellationToken);
        var jsonContent = JArray.Parse(fileContent);
        var registerAlternateKeysRequests = jsonContent.ToObject<List<RegisterAlternateKeyRequest>>() ?? [];

        if (!silent)
        {
            if (!AnsiConsole.Confirm("[bold red]WARNING: NON-SILENT IMPORTING FROM LARGE FILES MAY CAUSE HIGH WORKLOADS ON THE DATAHUB. ARE YOU SURE YOU WANT TO CONTINUE[/]"))
            {
                AnsiConsole.WriteLine();
                return 1;
            }
        }

        return await ProcessRegisterAlternateKeyRequests(registerAlternateKeysRequests, silent, @continue, Path.GetFileName(filePath), cancellationToken);
    }

    private async Task<int> RegisterAlternateKeysFromLocalFolder(string? path, string? pattern, bool silent, bool @continue, CancellationToken cancellationToken)
    {
        var folderPath = path ?? string.Empty;
        AnsiConsole.WriteLine();
        AnsiConsole.Markup($"[lime]Importing alternate keys from {folderPath} (press esc to cancel)[/]");
        AnsiConsole.WriteLine();

        if (!Directory.Exists(folderPath))
        {
            AnsiConsole.Markup("[red]Folder not found[/]");
            return 0;
        }

        if (!silent)
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
            var registerAlternateKeyRequests = jsonContent.ToObject<List<RegisterAlternateKeyRequest>>() ?? [];
            var result = await ProcessRegisterAlternateKeyRequests(registerAlternateKeyRequests, silent, @continue, Path.GetFileName(file), cancellationToken);
            if (result != 1 && !@continue)
            {
                return result;
            }
        }

        return 1;
    }

    private async Task<int> ProcessRegisterAlternateKeyRequests(List<RegisterAlternateKeyRequest> registerAlternateKeyRequests, bool silent, bool @continue, string label, CancellationToken cancellationToken)
    {
        try
        {
            var requestsToProcess = new List<RegisterAlternateKeyRequest>(registerAlternateKeyRequests);
            var totalCount = requestsToProcess.Count;
            var registeredCount = 0;
            var failures = new List<RegisterAlternateKeyFailure>();

            var result = await CliProgressHelper
                .RunAsync(async ctx =>
                {
                    var consoleTask = ctx.AddTask($"Processing {label} : {totalCount} items");
                    consoleTask.StartTask();

                    while (requestsToProcess.Count != 0)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            AnsiConsole.Markup("[lime]Cancelling...");
                            break;
                        }

                        var batch = requestsToProcess.Take(1000).ToList();

                        var sendResponse = (await _mediator.TrySend<SendResponse>(new SendRequest(new RegisterAlternateKeysRequest()
                        {
                            Requests = batch,
                            Silent = silent
                        }), cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var response } => response! };

                        var registerAlternateKeysResponse = sendResponse.Result.ToObject<RegisterAlternateKeysResponse>();

                        var responses = registerAlternateKeysResponse?.Responses ?? [];
                        registeredCount += responses.Count(w => w.Success);
                        var batchFailures = responses
                            .Select((response, index) => new { response, request = index < batch.Count ? batch[index] : null })
                            .Where(item => !item.response.Success)
                            .Select(item => RegisterAlternateKeyFailure.From(item.request, item.response))
                            .ToList();
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

                        requestsToProcess.RemoveRange(0, batch.Count);
                    }

                    consoleTask.Value = 100;
                    consoleTask.StopTask();
                    return 1;
                });

            AnsiConsole.MarkupLine($"Registered alternate keys for [green]{registeredCount}[/] of [yellow]{totalCount}[/] items from {Markup.Escape(label)}. Failed [red]{failures.Count}[/].");
            BulkOperationDetailsPrompt.Show(
                failures,
                [
                    nameof(RegisterAlternateKeyFailure.EntityType),
                    nameof(RegisterAlternateKeyFailure.Key),
                    nameof(RegisterAlternateKeyFailure.SourceEntityId),
                    nameof(RegisterAlternateKeyFailure.DataHubEntityId),
                    nameof(RegisterAlternateKeyFailure.FailureReason)
                ],
                "alternate-keys-register-errors",
                "failed alternate key registration result(s)");

            return result;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return 0;
        }
    }

    private sealed class RegisterAlternateKeyFailure
    {
        public string? EntityType { get; init; }
        public string? Key { get; init; }
        public string? SourceEntityId { get; init; }
        public string? DataHubEntityId { get; init; }
        public string? FailureReason { get; init; }

        public static RegisterAlternateKeyFailure From(RegisterAlternateKeyRequest? request, RegisterAlternateKeyResponse response)
        {
            return new RegisterAlternateKeyFailure
            {
                EntityType = request?.EntityType,
                Key = request?.Key,
                SourceEntityId = request?.SourceEntityId,
                DataHubEntityId = request?.DataHubEntityId,
                FailureReason = response.FailureReason
            };
        }
    }
}
