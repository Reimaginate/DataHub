using System.Text.Json.Nodes;
using FluentAssertions;
using Reimaginate.CLI.Base.Profiles;
using Reimaginate.DataHub.CLI.Tools.Shared.Auth;
using Reimaginate.DataHub.CLI.Tools.Shared.Contexts;
using Reimaginate.DataHub.CLI.Tools.Shared.Models;
using Reimaginate.DataHub.CLI.Tools.Shared.Runtime;
using Reimaginate.DataHub.CLI.Tools.Shared.Services.Connections;
using Xunit;

namespace Reimaginate.DataHub.CLI.Test.Unit;

public class ProfileAndConnectionAuthenticationTests
{
    [Fact]
    public void DataHubContextDescriptor_creates_valid_non_secret_target()
    {
        var descriptor = new DataHubContextDescriptor();

        var target = descriptor.CreateTarget(new Dictionary<string, string?>
        {
            ["url"] = "https://datahub.test/api/cli",
            ["tenantId"] = "tenant-1",
            ["scope"] = null
        });

        descriptor.ValidateTarget(target).IsValid.Should().BeTrue();
        target["url"]!.GetValue<string>().Should().Be("https://datahub.test/api/cli");
        target["tenantId"]!.GetValue<string>().Should().Be("tenant-1");
        target["scope"]!.GetValue<string>().Should().Be("api://7a3a7b0c-3f0b-43dd-b45f-487f1060ee91/datahub_cli");
        target.ToJsonString().Should().NotContain("AccessToken");
        target.ToJsonString().Should().NotContain("accessToken");
        target.ToJsonString().Should().NotContain("access-token");
    }

    [Fact]
    public void DataHubMsalClient_uses_fixed_public_client_id_when_scope_is_customized()
    {
        var client = new DataHubMsalClient(new DataHubConnection
        {
            Name = "custom",
            TenantId = "11111111-1111-1111-1111-111111111111",
            Scope = "api://customer-api/datahub_cli"
        });

        client.ClientId.Should().Be("7a3a7b0c-3f0b-43dd-b45f-487f1060ee91");
    }

    [Fact]
    public async Task ConnectionsService_requests_token_for_selected_shared_context_scope()
    {
        var resolver = CreateResolver(new SharedProfileDocument
        {
            CurrentProfile = "dev",
            Profiles =
            [
                new SharedProfile
                {
                    Name = "dev",
                    Targets =
                    {
                        [DataHubContextDescriptor.ToolIdValue] = JsonNode.Parse("""
                        {
                          "url": "https://datahub.test/api/cli",
                          "tenantId": "tenant-1",
                          "scope": "api://datahub-api/datahub_cli"
                        }
                        """)!
                    }
                }
            ]
        });
        var tokenProvider = new RecordingTokenProvider("access-token");
        var service = new ConnectionsService(resolver, tokenProvider);

        var connection = await service.EnsureConnected(CancellationToken.None);

        connection.Name.Should().Be("dev");
        connection.DataHubUrl.Should().Be("https://datahub.test/api/cli");
        connection.TenantId.Should().Be("tenant-1");
        connection.AccessToken.Should().Be("access-token");
        tokenProvider.RequestedConnection.Should().BeSameAs(connection);
        tokenProvider.RequestedMethod.Should().Be("silent");
        tokenProvider.RequestedConnection!.Scope.Should().Be("api://datahub-api/datahub_cli");
    }

    [Fact]
    public async Task ConnectionsService_ignores_stale_profile_auth_provider()
    {
        var resolver = CreateResolver(new SharedProfileDocument
        {
            CurrentProfile = "dev",
            Profiles =
            [
                new SharedProfile
                {
                    Name = "dev",
                    Targets =
                    {
                        [DataHubContextDescriptor.ToolIdValue] = JsonNode.Parse("""
                        {
                          "url": "https://datahub.test/api/cli",
                          "tenantId": "tenant-1",
                          "scope": "api://datahub-api/datahub_cli",
                          "authProvider": "azure-cli"
                        }
                        """)!
                    }
                }
            ]
        });
        var tokenProvider = new RecordingTokenProvider("access-token");
        var service = new ConnectionsService(resolver, tokenProvider);

        var connection = await service.EnsureConnected(CancellationToken.None);

        connection.Scope.Should().Be("api://datahub-api/datahub_cli");
        connection.AccessToken.Should().Be("access-token");
        tokenProvider.RequestedMethod.Should().Be("silent");
    }

    [Fact]
    public async Task ConnectionsService_command_url_override_preserves_selected_profile_auth_settings()
    {
        var previousTarget = CliTargetContext.Current;
        CliTargetContext.Current = new CliTargetOptions(
            Context: "dev",
            Url: "https://override.datahub.test/api/cli");
        var resolver = CreateResolver(new SharedProfileDocument
        {
            CurrentProfile = "dev",
            Profiles =
            [
                new SharedProfile
                {
                    Name = "dev",
                    Targets =
                    {
                        [DataHubContextDescriptor.ToolIdValue] = JsonNode.Parse("""
                        {
                          "url": "https://profile.datahub.test/api/cli",
                          "tenantId": "profile-tenant",
                          "scope": "api://profile-api/datahub_cli"
                        }
                        """)!
                    }
                }
            ]
        });

        try
        {
            var tokenProvider = new RecordingTokenProvider("access-token");
            var service = new ConnectionsService(resolver, tokenProvider);

            var connection = await service.EnsureConnected(CancellationToken.None);

            connection.Name.Should().Be("dev");
            connection.DataHubUrl.Should().Be("https://override.datahub.test/api/cli");
            connection.TenantId.Should().Be("profile-tenant");
            connection.Scope.Should().Be("api://profile-api/datahub_cli");
        }
        finally
        {
            CliTargetContext.Current = previousTarget;
        }
    }

    [Fact]
    public async Task ConnectionsService_command_target_options_override_selected_profile_values()
    {
        var previousTarget = CliTargetContext.Current;
        CliTargetContext.Current = new CliTargetOptions(
            Context: "dev",
            Url: "https://override.datahub.test/api/cli",
            TenantId: "override-tenant",
            Scope: "api://override-api/datahub_cli");
        var resolver = CreateResolver(new SharedProfileDocument
        {
            CurrentProfile = "dev",
            Profiles =
            [
                new SharedProfile
                {
                    Name = "dev",
                    Targets =
                    {
                        [DataHubContextDescriptor.ToolIdValue] = JsonNode.Parse("""
                        {
                          "url": "https://profile.datahub.test/api/cli",
                          "tenantId": "profile-tenant",
                          "scope": "api://profile-api/datahub_cli"
                        }
                        """)!
                    }
                }
            ]
        });

        try
        {
            var tokenProvider = new RecordingTokenProvider("access-token");
            var service = new ConnectionsService(resolver, tokenProvider);

            var connection = await service.EnsureConnected(CancellationToken.None);

            connection.Name.Should().Be("dev");
            connection.DataHubUrl.Should().Be("https://override.datahub.test/api/cli");
            connection.TenantId.Should().Be("override-tenant");
            connection.Scope.Should().Be("api://override-api/datahub_cli");
        }
        finally
        {
            CliTargetContext.Current = previousTarget;
        }
    }

    [Fact]
    public async Task ConnectionsService_environment_values_are_fallback_only_when_profile_resolves()
    {
        var previousTarget = CliTargetContext.Current;
        var previousUrl = Environment.GetEnvironmentVariable("DATAHUB_URL");
        var previousTenantId = Environment.GetEnvironmentVariable("DATAHUB_TENANT_ID");
        var previousScope = Environment.GetEnvironmentVariable("DATAHUB_SCOPE");
        CliTargetContext.Current = new CliTargetOptions(Context: "dev");
        Environment.SetEnvironmentVariable("DATAHUB_URL", "https://env.datahub.test/api/cli");
        Environment.SetEnvironmentVariable("DATAHUB_TENANT_ID", "env-tenant");
        Environment.SetEnvironmentVariable("DATAHUB_SCOPE", "api://env-api/datahub_cli");
        var resolver = CreateResolver(new SharedProfileDocument
        {
            CurrentProfile = "dev",
            Profiles =
            [
                new SharedProfile
                {
                    Name = "dev",
                    Targets =
                    {
                        [DataHubContextDescriptor.ToolIdValue] = JsonNode.Parse("""
                        {
                          "url": "https://profile.datahub.test/api/cli",
                          "tenantId": "profile-tenant",
                          "scope": "api://profile-api/datahub_cli"
                        }
                        """)!
                    }
                }
            ]
        });

        try
        {
            var tokenProvider = new RecordingTokenProvider("access-token");
            var service = new ConnectionsService(resolver, tokenProvider);

            var connection = await service.EnsureConnected(CancellationToken.None);

            connection.DataHubUrl.Should().Be("https://profile.datahub.test/api/cli");
            connection.TenantId.Should().Be("profile-tenant");
            connection.Scope.Should().Be("api://profile-api/datahub_cli");
        }
        finally
        {
            CliTargetContext.Current = previousTarget;
            Environment.SetEnvironmentVariable("DATAHUB_URL", previousUrl);
            Environment.SetEnvironmentVariable("DATAHUB_TENANT_ID", previousTenantId);
            Environment.SetEnvironmentVariable("DATAHUB_SCOPE", previousScope);
        }
    }

    [Fact]
    public async Task ConnectionsService_does_not_read_context_for_direct_target_options()
    {
        var previousTarget = CliTargetContext.Current;
        CliTargetContext.Current = new CliTargetOptions(
            Url: "https://datahub.test/api/cli",
            TenantId: "tenant-1",
            Scope: "api://datahub-api/datahub_cli");

        try
        {
            var tokenProvider = new RecordingTokenProvider("access-token");
            var service = new ConnectionsService(new ThrowingProfileResolver(), tokenProvider);

            var connection = await service.EnsureConnected(CancellationToken.None);

            connection.DataHubUrl.Should().Be("https://datahub.test/api/cli");
            connection.TenantId.Should().Be("tenant-1");
            connection.Scope.Should().Be("api://datahub-api/datahub_cli");
            connection.AccessToken.Should().Be("access-token");
        }
        finally
        {
            CliTargetContext.Current = previousTarget;
        }
    }

    [Fact]
    public async Task ConnectionsService_uses_inline_target_when_url_is_provided_and_profile_cannot_be_resolved()
    {
        var previousTarget = CliTargetContext.Current;
        var previousTenantId = Environment.GetEnvironmentVariable("DATAHUB_TENANT_ID");
        var previousScope = Environment.GetEnvironmentVariable("DATAHUB_SCOPE");
        CliTargetContext.Current = new CliTargetOptions(
            Context: "missing",
            Url: "https://override.datahub.test/api/cli");
        Environment.SetEnvironmentVariable("DATAHUB_TENANT_ID", "env-tenant");
        Environment.SetEnvironmentVariable("DATAHUB_SCOPE", "api://env-api/datahub_cli");

        try
        {
            var tokenProvider = new RecordingTokenProvider("access-token");
            var service = new ConnectionsService(new ThrowingProfileResolver(), tokenProvider);

            var connection = await service.EnsureConnected(CancellationToken.None);

            connection.Name.Should().Be("missing");
            connection.DataHubUrl.Should().Be("https://override.datahub.test/api/cli");
            connection.TenantId.Should().Be("env-tenant");
            connection.Scope.Should().Be("api://env-api/datahub_cli");
        }
        finally
        {
            CliTargetContext.Current = previousTarget;
            Environment.SetEnvironmentVariable("DATAHUB_TENANT_ID", previousTenantId);
            Environment.SetEnvironmentVariable("DATAHUB_SCOPE", previousScope);
        }
    }

    [Fact]
    public async Task ConnectionsService_ignores_environment_auth_provider_to_direct_target()
    {
        var previousTarget = CliTargetContext.Current;
        var previousEnvironment = Environment.GetEnvironmentVariable("DATAHUB_AUTH_PROVIDER");
        CliTargetContext.Current = new CliTargetOptions(
            Url: "https://datahub.test/api/cli",
            TenantId: "tenant-1",
            Scope: "api://datahub-api/datahub_cli");
        Environment.SetEnvironmentVariable("DATAHUB_AUTH_PROVIDER", "azure-cli");

        try
        {
            var tokenProvider = new RecordingTokenProvider("access-token");
            var service = new ConnectionsService(new ThrowingProfileResolver(), tokenProvider);

            var connection = await service.EnsureConnected(CancellationToken.None);

            connection.Scope.Should().Be("api://datahub-api/datahub_cli");
            connection.AccessToken.Should().Be("access-token");
            tokenProvider.RequestedMethod.Should().Be("silent");
        }
        finally
        {
            Environment.SetEnvironmentVariable("DATAHUB_AUTH_PROVIDER", previousEnvironment);
            CliTargetContext.Current = previousTarget;
        }
    }

    [Fact]
    public async Task MsalDataHubCliTokenProvider_attempts_silent_then_browser()
    {
        var msalClient = new RecordingMsalClient
        {
            SilentToken = null,
            InteractiveToken = "interactive-token"
        };
        var provider = new MsalDataHubCliTokenProvider(_ => msalClient);
        var connection = new DataHubConnection
        {
            Name = "dev",
            TenantId = "tenant-1",
            Scope = "api://datahub-api/datahub_cli"
        };

        var token = await provider.GetAccessTokenAsync(connection, CancellationToken.None);

        token.Should().Be("interactive-token");
        msalClient.Calls.Should().Equal("silent", "interactive");
    }

    [Fact]
    public async Task MsalDataHubCliTokenProvider_silent_status_does_not_attempt_interactive_login()
    {
        var msalClient = new RecordingMsalClient
        {
            SilentToken = "silent-token"
        };
        var provider = new MsalDataHubCliTokenProvider(_ => msalClient);
        var connection = new DataHubConnection
        {
            Name = "dev",
            TenantId = "tenant-1",
            Scope = "api://datahub-api/datahub_cli"
        };

        var token = await provider.GetAccessTokenSilentAsync(connection, CancellationToken.None);

        token.Should().Be("silent-token");
        msalClient.Calls.Should().Equal("silent");
    }

    [Fact]
    public async Task MsalDataHubCliTokenProvider_silent_status_tells_user_to_run_login_when_no_cached_token_exists()
    {
        var msalClient = new RecordingMsalClient
        {
            SilentToken = null
        };
        var provider = new MsalDataHubCliTokenProvider(_ => msalClient);
        var connection = new DataHubConnection
        {
            Name = "dev",
            TenantId = "tenant-1",
            Scope = "api://datahub-api/datahub_cli"
        };

        var act = async () => await provider.GetAccessTokenSilentAsync(connection, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Run 'datahub login --profile dev'.*");
        msalClient.Calls.Should().Equal("silent");
    }

    [Fact]
    public async Task MsalDataHubCliTokenProvider_login_attempts_browser_only()
    {
        var msalClient = new RecordingMsalClient
        {
            SilentToken = "silent-token",
            InteractiveToken = "interactive-token"
        };
        var provider = new MsalDataHubCliTokenProvider(_ => msalClient);
        var connection = new DataHubConnection
        {
            Name = "dev",
            TenantId = "tenant-1",
            Scope = "api://datahub-api/datahub_cli"
        };

        var token = await provider.LoginAsync(connection, CancellationToken.None);

        token.Should().Be("interactive-token");
        msalClient.Calls.Should().Equal("interactive");
    }

    [Fact]
    public async Task MsalDataHubCliTokenProvider_login_reports_failure_when_interactive_token_is_not_persisted()
    {
        var msalClient = new RecordingMsalClient
        {
            InteractiveToken = "interactive-token",
            PersistInteractiveToken = false
        };
        var provider = new MsalDataHubCliTokenProvider(_ => msalClient);
        var connection = new DataHubConnection
        {
            Name = "dev",
            TenantId = "tenant-1",
            Scope = "api://datahub-api/datahub_cli"
        };

        var act = async () => await provider.LoginAsync(connection, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*no usable token was persisted to the MSAL cache*");
        msalClient.Calls.Should().Equal("interactive");
        msalClient.CacheCheckCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task MsalDataHubCliTokenProvider_login_reports_failure_when_browser_flow_stalls_without_cached_token()
    {
        var msalClient = new RecordingMsalClient
        {
            InteractiveNeverCompletes = true
        };
        var provider = new MsalDataHubCliTokenProvider(_ => msalClient, TimeSpan.FromMilliseconds(20));
        var connection = new DataHubConnection
        {
            Name = "dev",
            TenantId = "tenant-1",
            Scope = "api://datahub-api/datahub_cli"
        };

        var act = async () => await provider.LoginAsync(connection, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("MSAL authentication failed for DataHub target 'dev'.*");
        msalClient.Calls.Should().Equal("interactive", "silent");
    }

    [Fact]
    public async Task MsalDataHubCliTokenProvider_login_uses_cached_token_when_browser_flow_stalls_after_sign_in()
    {
        var msalClient = new RecordingMsalClient
        {
            InteractiveNeverCompletes = true,
            SilentToken = "cached-token"
        };
        var provider = new MsalDataHubCliTokenProvider(_ => msalClient, TimeSpan.FromMilliseconds(20));
        var connection = new DataHubConnection
        {
            Name = "dev",
            TenantId = "tenant-1",
            Scope = "api://datahub-api/datahub_cli"
        };

        var token = await provider.LoginAsync(connection, CancellationToken.None);

        token.Should().Be("cached-token");
        msalClient.Calls.Should().Equal("interactive", "silent");
    }

    [Fact]
    public async Task MsalDataHubCliTokenProvider_logout_clears_msal_cache()
    {
        var msalClient = new RecordingMsalClient();
        var provider = new MsalDataHubCliTokenProvider(_ => msalClient);
        var connection = new DataHubConnection
        {
            Name = "dev",
            TenantId = "tenant-1",
            Scope = "api://datahub-api/datahub_cli"
        };

        await provider.LogoutAsync(connection, CancellationToken.None);

        msalClient.Calls.Should().Equal("clear");
    }

    private static ProfileResolver CreateResolver(SharedProfileDocument document)
    {
        var store = new InMemoryProfileStore(document);
        return new ProfileResolver(store, [new DataHubContextDescriptor()], new FakeProfileEnvironment());
    }

    private sealed class InMemoryProfileStore : IProfileStore
    {
        private readonly SharedProfileDocument _document;

        public InMemoryProfileStore(SharedProfileDocument document)
        {
            _document = document;
        }

        public string StorePath => "memory://contexts.json";

        public Task<SharedProfileDocument> ReadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_document);

        public Task WriteAsync(SharedProfileDocument document, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeProfileEnvironment : IProfileEnvironment
    {
        public string? GetEnvironmentVariable(string name) => null;
    }

    private sealed class ThrowingProfileResolver : IProfileResolver
    {
        public Task<ResolvedToolProfileTarget> ResolveTargetAsync(
            string toolId,
            string? profileName = null,
            CancellationToken cancellationToken = default)
            => throw new IOException("Shared context storage should not be read for a direct target.");
    }

    private sealed class RecordingTokenProvider : IDataHubCliTokenProvider
    {
        private readonly string _token;

        public RecordingTokenProvider(string token)
        {
            _token = token;
        }

        public DataHubConnection? RequestedConnection { get; private set; }
        public string? RequestedMethod { get; private set; }

        public Task<string> GetAccessTokenAsync(DataHubConnection connection, CancellationToken cancellationToken)
        {
            RequestedConnection = connection;
            RequestedMethod = "interactive";
            return Task.FromResult(_token);
        }

        public Task<string> GetAccessTokenSilentAsync(DataHubConnection connection, CancellationToken cancellationToken)
        {
            RequestedConnection = connection;
            RequestedMethod = "silent";
            return Task.FromResult(_token);
        }

        public Task<string> LoginAsync(DataHubConnection connection, CancellationToken cancellationToken)
        {
            RequestedConnection = connection;
            RequestedMethod = "login";
            return Task.FromResult(_token);
        }

        public Task LogoutAsync(DataHubConnection connection, CancellationToken cancellationToken)
        {
            RequestedConnection = connection;
            RequestedMethod = "logout";
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingMsalClient : IDataHubMsalClient
    {
        public List<string> Calls { get; } = [];
        public string? SilentToken { get; set; }
        public string InteractiveToken { get; set; } = "interactive-token";
        public Exception? InteractiveException { get; set; }
        public bool InteractiveNeverCompletes { get; set; }
        public bool PersistInteractiveToken { get; set; } = true;
        public int CacheCheckCount { get; private set; }

        public Task<string?> AcquireTokenSilentAsync(string[] scopes, CancellationToken cancellationToken)
        {
            Calls.Add("silent");
            return Task.FromResult(SilentToken);
        }

        public Task<string> AcquireTokenInteractiveAsync(string[] scopes, CancellationToken cancellationToken)
        {
            Calls.Add("interactive");
            if (InteractiveNeverCompletes)
            {
                return new TaskCompletionSource<string>().Task;
            }

            if (InteractiveException == null && PersistInteractiveToken)
            {
                SilentToken = InteractiveToken;
            }

            return InteractiveException == null
                ? Task.FromResult(InteractiveToken)
                : Task.FromException<string>(InteractiveException);
        }

        public Task<bool> HasCachedTokenAsync(string[] scopes, CancellationToken cancellationToken)
        {
            CacheCheckCount++;
            return Task.FromResult(!string.IsNullOrWhiteSpace(SilentToken));
        }

        public Task ClearTokenCacheAsync(CancellationToken cancellationToken)
        {
            Calls.Add("clear");
            return Task.CompletedTask;
        }
    }
}
