using System.CommandLine;
using Reimaginate.DataHub.CLI.Tools.Shared.Runtime;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Active;

internal static class CommandContextOptions
{
    public static void AddTargetOptions(Command command, bool includeTenantIdAlias = true)
    {
        var profileOption = new Option<string?>("--profile") { Description = "Shared Reimaginate CLI profile to use for this command." };
        profileOption.Aliases.Add("--context");
        var tenantOption = new Option<string?>("--tenant") { Description = "Microsoft Entra tenant id for this command." };
        if (includeTenantIdAlias)
        {
            tenantOption.Aliases.Add("--tenant-id");
        }

        command.Add(profileOption);
        command.Add(new Option<string?>("--url") { Description = "DataHub CLI endpoint URL for this command." });
        command.Add(tenantOption);
        command.Add(new Option<string?>("--scope") { Description = "DataHub API delegated scope for this command." });
    }

    public static CliTargetOptions CreateTargetOptions(ParseResult parseResult)
        => new(
            parseResult.GetValue<string?>("--profile"),
            parseResult.GetValue<string?>("--url"),
            parseResult.GetValue<string?>("--tenant"),
            parseResult.GetValue<string?>("--scope"));

    public static CliOutputFormat ParseOutputFormat(string? value)
        => value?.Trim().ToLowerInvariant() switch
        {
            null or "" or "table" => CliOutputFormat.Table,
            "json" => CliOutputFormat.Json,
            "ndjson" => CliOutputFormat.Ndjson,
            "tsv" => CliOutputFormat.Tsv,
            _ => throw new ArgumentException($"Unsupported output format '{value}'. Use table, json, ndjson, or tsv.")
        };
}
