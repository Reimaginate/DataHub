using Reimaginate.CLI.Base.Profiles;
using Reimaginate.DataHub.CLI.Tools.Shared.Auth;
using Reimaginate.DataHub.CLI.Tools.Shared.Contexts;
using Reimaginate.DataHub.CLI.Tools.Shared.Models;
using Reimaginate.DataHub.CLI.Tools.Shared.Runtime;

namespace Reimaginate.DataHub.CLI.Tools.Shared.Services.Connections;

public interface IConnectionsService
{
    Task<DataHubConnection> ResolveConnectionAsync(CancellationToken cancellationToken);
    Task<DataHubConnection> EnsureConnected(CancellationToken cancellationToken);
    DataHubConnection CurrentDataHubConnection { get; set; }
}

public class ConnectionsService(IProfileResolver profileResolver, IDataHubCliTokenProvider tokenProvider)
    : IConnectionsService
{
    public async Task<DataHubConnection> ResolveConnectionAsync(CancellationToken cancellationToken)
    {
        var target = CliTargetContext.Current;
        var profileConnection = await TryResolveProfileConnectionAsync(target, cancellationToken);
        var dataHubConnection = profileConnection != null
            ? ApplyTargetOverrides(profileConnection, target)
            : ResolveDirectTarget(target)
              ?? DataHubContextDescriptor.CreateConnection(
                  await profileResolver.ResolveTargetAsync(
                      DataHubContextDescriptor.ToolIdValue,
                      target?.Context,
                      cancellationToken));

        CurrentDataHubConnection = dataHubConnection;
        return CurrentDataHubConnection;
    }

    public async Task<DataHubConnection> EnsureConnected(CancellationToken cancellationToken)
    {
        var dataHubConnection = await ResolveConnectionAsync(cancellationToken);
        dataHubConnection.AccessToken = await tokenProvider.GetAccessTokenSilentAsync(dataHubConnection, cancellationToken);

        CurrentDataHubConnection = dataHubConnection;
        return CurrentDataHubConnection;
    }

    public DataHubConnection CurrentDataHubConnection { get; set; } = new();

    private async Task<DataHubConnection?> TryResolveProfileConnectionAsync(
        CliTargetOptions? target,
        CancellationToken cancellationToken)
    {
        try
        {
            return DataHubContextDescriptor.CreateConnection(
                await profileResolver.ResolveTargetAsync(
                    DataHubContextDescriptor.ToolIdValue,
                    target?.Context,
                    cancellationToken));
        }
        catch when (HasDirectTargetUrl(target))
        {
            return null;
        }
    }

    private static DataHubConnection ApplyTargetOverrides(DataHubConnection profileConnection, CliTargetOptions? target)
    {
        return new DataHubConnection
        {
            Name = FirstNonEmpty(target?.Context, profileConnection.Name, "inline")!,
            DataHubUrl = FirstNonEmpty(target?.Url, profileConnection.DataHubUrl, Environment.GetEnvironmentVariable("DATAHUB_URL"))!,
            TenantId = FirstNonEmpty(target?.TenantId, profileConnection.TenantId, Environment.GetEnvironmentVariable("DATAHUB_TENANT_ID")) ?? string.Empty,
            Scope = FirstNonEmpty(target?.Scope, profileConnection.Scope, Environment.GetEnvironmentVariable("DATAHUB_SCOPE"), DataHubCliAuthenticationDefaults.Scope)!
        };
    }

    private static DataHubConnection? ResolveDirectTarget(CliTargetOptions? target)
    {
        var envUrl = Environment.GetEnvironmentVariable("DATAHUB_URL");
        var envTenantId = Environment.GetEnvironmentVariable("DATAHUB_TENANT_ID");
        var envScope = Environment.GetEnvironmentVariable("DATAHUB_SCOPE");

        var directUrl = FirstNonEmpty(target?.Url, envUrl);
        if (!string.IsNullOrWhiteSpace(directUrl))
        {
            return new DataHubConnection
            {
                Name = FirstNonEmpty(target?.Context, "inline")!,
                DataHubUrl = directUrl!,
                TenantId = FirstNonEmpty(target?.TenantId, envTenantId) ?? string.Empty,
                Scope = FirstNonEmpty(target?.Scope, envScope, DataHubCliAuthenticationDefaults.Scope)!
            };
        }

        return null;
    }

    private static bool HasDirectTargetUrl(CliTargetOptions? target)
        => !string.IsNullOrWhiteSpace(target?.Url) ||
           !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DATAHUB_URL"));

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }
}
