using FluentAssertions;
using OneOf;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.DataAccess.Queries.GetCosmosDocuments;
using Reimaginate.DataHub.Requests.External.CLI.GetTrace;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;
using Xunit;
using Reimaginate.DataHub.Test.Unit.Base;
using Reimaginate.Test.Framework;

namespace Reimaginate.DataHub.Test.Unit.Tests;

public class AuthorizationTests : ScenarioUnitTestBase
{
    [Fact]
    public async Task Permission_catalog_exposes_known_datahub_permissions()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Permission_catalog_exposes_known_datahub_permissions)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Permission_catalog_exposes_known_datahub_permissions))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    DataHubPermissionCatalog.GetPermissions().Should().Contain([
                        DataHubPermissions.QueryUsers,
                        DataHubPermissions.QueryRoles,
                        DataHubPermissions.RegisterRoles,
                        DataHubPermissions.UpdateRoles,
                        DataHubPermissions.DeleteRoles,
                        DataHubPermissions.QueryPermissions,
                        DataHubPermissions.QueryDiagnostics
                    ]);

                    return await ActionResult(currentObject, stash);
                });
            })
            .Then("The assertions complete successfully", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .Run();
    }

    [Fact]
    public async Task Permission_catalog_rejects_unknown_permissions()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Permission_catalog_rejects_unknown_permissions)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Permission_catalog_rejects_unknown_permissions))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    DataHubPermissionCatalog.GetUnknownPermissions([DataHubPermissions.QueryUsers, "unknown:permission"])
                        .Should().Equal("unknown:permission");

                    return await ActionResult(currentObject, stash);
                });
            })
            .Then("The assertions complete successfully", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .Run();
    }

    [Fact]
    public async Task Built_in_admin_grants_all_permissions()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Built_in_admin_grants_all_permissions)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Built_in_admin_grants_all_permissions))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var user = new User { Roles = [DataHubRoles.Admin] };

                    Authorization.HasPermissions(user, DataHubPermissions.DeleteRoles).Should().BeTrue();
                    Authorization.HasPermissions(user, DataHubPermissions.QueryDiagnostics).Should().BeTrue();

                    return await ActionResult(currentObject, stash);
                });
            })
            .Then("The assertions complete successfully", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .Run();
    }

    [Fact]
    public async Task Built_in_reader_grants_only_reader_permissions()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Built_in_reader_grants_only_reader_permissions)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Built_in_reader_grants_only_reader_permissions))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var user = new User { Roles = [DataHubRoles.Reader] };

                    Authorization.HasPermissions(user, DataHubPermissions.QueryEntities).Should().BeTrue();
                    Authorization.HasPermissions(user, DataHubPermissions.UpdateUsers).Should().BeFalse();
                    Authorization.HasPermissions(user, DataHubPermissions.QueryDiagnostics).Should().BeFalse();

                    return await ActionResult(currentObject, stash);
                });
            })
            .Then("The assertions complete successfully", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .Run();
    }

    [Fact]
    public async Task Tenant_role_grants_configured_permissions()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Tenant_role_grants_configured_permissions)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Tenant_role_grants_configured_permissions))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var mediator = new RoleMediator([
                        new DataHubRole
                        {
                            TenantId = "tenant-1",
                            Name = "operators",
                            Permissions = [DataHubPermissions.QueryUsers, DataHubPermissions.UpdateUsers]
                        }
                    ]);
                    var service = new DataHubAuthorizationService(mediator);
                    var user = new User { TenantId = "tenant-1", Roles = ["operators"] };

                    var resolved = await service.ResolveEffectivePermissionsAsync(user, CancellationToken.None);

                    resolved.Should().NotBeNull();
                    resolved!.EffectivePermissions.Should().Equal(DataHubPermissions.QueryUsers, DataHubPermissions.UpdateUsers);

                    return await ActionResult(currentObject, stash);
                });
            })
            .Then("The assertions complete successfully", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .Run();
    }

    [Fact]
    public async Task Built_in_and_tenant_roles_combine()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Built_in_and_tenant_roles_combine)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Built_in_and_tenant_roles_combine))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var mediator = new RoleMediator([
                        new DataHubRole
                        {
                            TenantId = "tenant-1",
                            Name = "operators",
                            Permissions = [DataHubPermissions.UpdateUsers]
                        }
                    ]);
                    var service = new DataHubAuthorizationService(mediator);
                    var user = new User { TenantId = "tenant-1", Roles = [DataHubRoles.Reader, "operators"] };

                    var resolved = await service.ResolveEffectivePermissionsAsync(user, CancellationToken.None);

                    resolved.Should().NotBeNull();
                    resolved!.EffectivePermissions.Should().Contain([DataHubPermissions.QueryEntities, DataHubPermissions.UpdateUsers]);

                    return await ActionResult(currentObject, stash);
                });
            })
            .Then("The assertions complete successfully", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .Run();
    }

    [Fact]
    public async Task Unknown_role_fails_authorization_resolution()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Unknown_role_fails_authorization_resolution)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Unknown_role_fails_authorization_resolution))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var service = new DataHubAuthorizationService(new RoleMediator([]));
                    var user = new User { TenantId = "tenant-1", Roles = ["missing-role"] };

                    var resolved = await service.ResolveEffectivePermissionsAsync(user, CancellationToken.None);

                    resolved.Should().BeNull();

                    return await ActionResult(currentObject, stash);
                });
            })
            .Then("The assertions complete successfully", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .Run();
    }

    [Fact]
    public void GetTraceRequestValidator_requires_query_diagnostics_permission_and_correlation_id()
    {
        var validator = new GetTraceRequestValidator();

        var denied = validator.Validate(new GetTraceRequest
        {
            TraceCorrelationId = "corr-1",
            User = new User { EffectivePermissions = [DataHubPermissions.QueryEntities] }
        });
        denied.IsValid.Should().BeFalse();
        denied.Errors.Should().Contain(error => error.ErrorMessage == "Not Authorized");

        var missingCorrelation = validator.Validate(new GetTraceRequest
        {
            User = new User { EffectivePermissions = [DataHubPermissions.QueryDiagnostics] }
        });
        missingCorrelation.IsValid.Should().BeFalse();
        missingCorrelation.Errors.Should().Contain(error => error.PropertyName == nameof(GetTraceRequest.TraceCorrelationId));

        var allowed = validator.Validate(new GetTraceRequest
        {
            TraceCorrelationId = "corr-1",
            User = new User { EffectivePermissions = [DataHubPermissions.QueryDiagnostics] }
        });
        allowed.IsValid.Should().BeTrue();
    }

    private sealed class RoleMediator : IMediator
    {
        private readonly List<DataHubRole> _roles;

        public RoleMediator(IEnumerable<DataHubRole> roles)
        {
            _roles = roles.ToList();
        }

        public Task<OneOf<TResponse, Exception>> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<OneOf<object, Exception>> SendAsync(IRequest request, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<object> SendAndHandleExceptions<TRequest>(TRequest request, CancellationToken cancellationToken, Action<Exception>? exceptionHandler = null)
            where TRequest : IRequest
        {
            throw new NotSupportedException();
        }

        public Task<TResponse> SendAndHandleExceptions<TResponse>(IRequest request, CancellationToken cancellationToken, Action<Exception>? exceptionHandler = null)
        {
            throw new NotSupportedException();
        }

        public Task<(TResponse? Response, Exception? Exception)> TrySend<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken, Action<Exception>? exceptionHandler = null)
        {
            if (request is GetCosmosDocumentsQuery<DataHubRole> query)
            {
                var tenantId = query.Parameters?.FirstOrDefault(parameter => parameter.Name == "tenantId")?.Value?.ToString();
                var roleName = query.Parameters?.FirstOrDefault(parameter => parameter.Name == "roleName")?.Value?.ToString();
                var results = _roles
                    .Where(role => query.WhereClause == "x.TenantId = @tenantId and x.Name = @roleName"
                                   && role.TenantId == tenantId
                                   && role.Name == roleName)
                    .ToList();

                object response = new PagedResults<DataHubRole>
                {
                    Results = results,
                    ResultCount = results.Count
                };

                return Task.FromResult(((TResponse?)response, (Exception?)null));
            }

            throw new NotSupportedException(request.GetType().FullName);
        }
    }
}
