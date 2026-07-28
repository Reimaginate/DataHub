using System.Text.Json.Nodes;
using Reimaginate.CLI.Base.Profiles;
using Reimaginate.DataHub.CLI.Tools.Shared.Auth;
using Reimaginate.DataHub.CLI.Tools.Shared.Models;

namespace Reimaginate.DataHub.CLI.Tools.Shared.Contexts;

public sealed class DataHubContextDescriptor : IToolProfileDescriptor
{
    public const string ToolIdValue = "datahub";

    public string ToolId => ToolIdValue;
    public string DisplayName => "DataHub";

    public IReadOnlyList<ToolProfileFieldDefinition> Fields { get; } =
    [
        new()
        {
            FieldName = "url",
            OptionName = "url",
            Description = "DataHub CLI endpoint URL."
        },
        new()
        {
            FieldName = "tenantId",
            OptionName = "tenant",
            Description = "Microsoft Entra tenant id.",
            Aliases = ["tenant-id"]
        },
        new()
        {
            FieldName = "scope",
            OptionName = "scope",
            Description = "DataHub API delegated scope.",
            Required = false
        }
    ];

    public JsonObject CreateTarget(IReadOnlyDictionary<string, string?> optionValues)
    {
        var target = new JsonObject
        {
            ["url"] = optionValues.GetValueOrDefault("url"),
            ["tenantId"] = optionValues.GetValueOrDefault("tenantId"),
            ["scope"] = string.IsNullOrWhiteSpace(optionValues.GetValueOrDefault("scope"))
                ? DataHubCliAuthenticationDefaults.Scope
                : optionValues.GetValueOrDefault("scope")
        };

        return target;
    }

    public ToolProfileValidationResult ValidateTarget(JsonObject target)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(GetString(target, "url")))
        {
            errors.Add("Missing DataHub target field 'url'.");
        }

        if (string.IsNullOrWhiteSpace(GetString(target, "tenantId")))
        {
            errors.Add("Missing DataHub target field 'tenantId'.");
        }

        if (string.IsNullOrWhiteSpace(GetString(target, "scope")))
        {
            target["scope"] = DataHubCliAuthenticationDefaults.Scope;
        }

        return errors.Count == 0
            ? ToolProfileValidationResult.Success()
            : ToolProfileValidationResult.Failure(errors.ToArray());
    }

    public static DataHubConnection CreateConnection(ResolvedToolProfileTarget resolved)
    {
        var target = resolved.Target;
        return new DataHubConnection
        {
            Name = resolved.ProfileName,
            DataHubUrl = GetRequiredString(target, "url"),
            TenantId = GetRequiredString(target, "tenantId"),
            Scope = GetString(target, "scope") ?? DataHubCliAuthenticationDefaults.Scope
        };
    }

    private static string GetRequiredString(JsonObject target, string fieldName)
        => GetString(target, fieldName)
           ?? throw new InvalidOperationException($"Missing DataHub target field '{fieldName}'.");

    private static string? GetString(JsonObject target, string fieldName)
        => target[fieldName]?.GetValue<string>();
}
