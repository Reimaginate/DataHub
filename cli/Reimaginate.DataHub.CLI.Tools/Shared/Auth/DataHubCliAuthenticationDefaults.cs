namespace Reimaginate.DataHub.CLI.Tools.Shared.Auth;

public static class DataHubCliAuthenticationDefaults
{
    public const string ClientId = "7a3a7b0c-3f0b-43dd-b45f-487f1060ee91";
    public const string ScopeName = "datahub_cli";
    public const string Audience = $"api://{ClientId}";
    public const string Scope = $"{Audience}/{ScopeName}";

    [Obsolete("Use ClientId instead.")]
    public const string DataHubApiClientId = ClientId;
}
