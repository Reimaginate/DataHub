using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.CLI.Tools.Shared.Helpers;
using Reimaginate.DataHub.CLI.Tools.Shared.Runtime;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Spectre.Console;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Active;

internal sealed class EntitiesSplitAltKeyCommand : Command
{
    private readonly ICLIApi _api;
    private readonly Argument<string> _entityTypeArgument = new("entity-type")
    {
        Description = "DataHub entity type."
    };
    private readonly Argument<string> _entityIdArgument = new("entity-id")
    {
        Description = "DataHub entity id."
    };
    private readonly Option<string> _keyOption = new("--key")
    {
        Description = "Alternate key name."
    };
    private readonly Option<string> _valueOption = new("--value")
    {
        Description = "Alternate key value."
    };
    private readonly Option<bool> _silentOption = new("--silent")
    {
        Description = "Split without updating original entity tracking or last updated timestamp."
    };
    private readonly Option<bool> _dryRunOption = new("--dry-run")
    {
        Description = "Preview the split without writing entity or tracking data."
    };
    private readonly Option<bool> _yesOption = new("--yes")
    {
        Description = "Skip confirmation prompt."
    };
    private readonly Option<string?> _outputOption = new("--output")
    {
        Description = "Output format: table, json, ndjson, or tsv."
    };

    public EntitiesSplitAltKeyCommand(IServiceProvider serviceProvider) : base("split-alt-key", "Split one duplicate alternate key into a new cloned DataHub entity.")
    {
        _api = serviceProvider.GetRequiredService<ICLIApi>();
        _keyOption.Required = true;
        _valueOption.Required = true;

        CommandContextOptions.AddTargetOptions(this);
        Add(_outputOption);
        Add(_entityTypeArgument);
        Add(_entityIdArgument);
        Add(_keyOption);
        Add(_valueOption);
        Add(_silentOption);
        Add(_dryRunOption);
        Add(_yesOption);

        SetAction(InvokeAsync);
    }

    private async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var previousTarget = CliTargetContext.Current;
        var previousOutputFormat = CliOutputContext.Format;
        CliTargetContext.Current = CommandContextOptions.CreateTargetOptions(parseResult);
        CliOutputContext.Format = CommandContextOptions.ParseOutputFormat(parseResult.GetValue(_outputOption));

        try
        {
            var entityType = parseResult.GetValue(_entityTypeArgument)!;
            var entityId = parseResult.GetValue(_entityIdArgument)!;
            var key = parseResult.GetValue(_keyOption)!;
            var value = parseResult.GetValue(_valueOption)!;
            var silent = parseResult.GetValue(_silentOption);
            var dryRun = parseResult.GetValue(_dryRunOption);
            var yes = parseResult.GetValue(_yesOption);

            if (!dryRun && !yes && !AnsiConsole.Confirm($"Split alternate key '{key}' with value '{value}' from {entityType} entity '{entityId}'?"))
            {
                return CliExitCodes.Cancelled;
            }

            var response = await Post<SplitDataHubEntityAlternateKeyResponse>(new SplitDataHubEntityAlternateKeyRequest
            {
                EntityType = entityType,
                EntityId = entityId,
                Key = key,
                Value = value,
                Silent = silent,
                DryRun = dryRun
            }, cancellationToken);

            if (dryRun && CliOutputContext.Format == CliOutputFormat.Table)
            {
                AnsiConsole.MarkupLine("[yellow]Dry run only. No entity or tracking data was written.[/]");
            }

            WriteResult(response);
            return response.Success ? CliExitCodes.Success : CliExitCodes.Failure;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return CliExitCodes.Failure;
        }
        finally
        {
            CliTargetContext.Current = previousTarget;
            CliOutputContext.Format = previousOutputFormat;
        }
    }

    private async Task<T> Post<T>(DataHubCLIRequest request, CancellationToken cancellationToken)
    {
        request.CorrelationId ??= Guid.NewGuid().ToString("N");
        request.RequestType ??= request.GetType().Name;

        return await _api.PostAdminMessage<T>(new SerializedRequest
        {
            RequestType = request.RequestType,
            CorrelationId = request.CorrelationId,
            Data = JsonConvert.SerializeObject(request)
        }, cancellationToken);
    }

    private static void WriteResult(SplitDataHubEntityAlternateKeyResponse response)
    {
        ConsoleHelper.PrintTable([response], [
            nameof(SplitDataHubEntityAlternateKeyResponse.Success),
            nameof(SplitDataHubEntityAlternateKeyResponse.Changed),
            nameof(SplitDataHubEntityAlternateKeyResponse.EntityType),
            nameof(SplitDataHubEntityAlternateKeyResponse.OriginalEntityId),
            nameof(SplitDataHubEntityAlternateKeyResponse.NewEntityId),
            nameof(SplitDataHubEntityAlternateKeyResponse.Key),
            nameof(SplitDataHubEntityAlternateKeyResponse.Value),
            nameof(SplitDataHubEntityAlternateKeyResponse.CopiedTrackingEntries),
            nameof(SplitDataHubEntityAlternateKeyResponse.FailureReason)
        ]);
    }

}
