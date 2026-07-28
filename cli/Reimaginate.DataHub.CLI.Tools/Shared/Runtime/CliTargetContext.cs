namespace Reimaginate.DataHub.CLI.Tools.Shared.Runtime;

public sealed record CliTargetOptions(
    string? Context = null,
    string? Url = null,
    string? TenantId = null,
    string? Scope = null);

public static class CliTargetContext
{
    private static readonly AsyncLocal<CliTargetOptions?> CurrentOptions = new();

    public static CliTargetOptions? Current
    {
        get => CurrentOptions.Value;
        set => CurrentOptions.Value = value;
    }
}
