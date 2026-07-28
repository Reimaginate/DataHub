using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Reimaginate.DataHub.CLI.Tools.Commands.Delete.Role;
using Reimaginate.DataHub.CLI.Tools.Commands.Get.Permissions;
using Reimaginate.DataHub.CLI.Tools.Commands.Get.Roles;
using Reimaginate.DataHub.CLI.Tools.Commands.Register.Role;
using Reimaginate.DataHub.CLI.Tools.Commands.Update.Role;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Xunit;

namespace Reimaginate.DataHub.CLI.Test.Unit;

public class RoleCommandTests
{
    [Fact]
    public async Task RegisterRoleCommand_builds_role_request()
    {
        var api = new CapturingCliApi(new RegisterRoleResponse { Success = true });
        var command = new RegisterRoleCommand(CreateServiceProvider(api));

        var exitCode = await command.HandleCommand("Operators", "query:users, update:users", "Operator role", CancellationToken.None);

        exitCode.Should().Be(1);
        api.Message!.RequestType.Should().Be(nameof(RegisterRoleRequest));
        var request = JsonConvert.DeserializeObject<RegisterRoleRequest>(api.Message.Data)!;
        request.Name.Should().Be("Operators");
        request.Description.Should().Be("Operator role");
        request.Permissions.Should().Equal("query:users", "update:users");
    }

    [Fact]
    public async Task UpdateRoleCommand_builds_role_request()
    {
        var api = new CapturingCliApi(new UpdateRoleResponse { Success = true });
        var command = new UpdateRoleCommand(CreateServiceProvider(api));

        var exitCode = await command.HandleCommand("operators", "query:users", "Updated role", CancellationToken.None);

        exitCode.Should().Be(1);
        api.Message!.RequestType.Should().Be(nameof(UpdateRoleRequest));
        var request = JsonConvert.DeserializeObject<UpdateRoleRequest>(api.Message.Data)!;
        request.Name.Should().Be("operators");
        request.Description.Should().Be("Updated role");
        request.Permissions.Should().Equal("query:users");
    }

    [Fact]
    public async Task DeleteRoleCommand_builds_delete_role_request()
    {
        var api = new CapturingCliApi(new DeleteRoleResponse { Success = true });
        var command = new DeleteRoleCommand(CreateServiceProvider(api));

        var exitCode = await command.HandleCommand("operators", CancellationToken.None);

        exitCode.Should().Be(1);
        api.Message!.RequestType.Should().Be(nameof(DeleteRoleRequest));
        JsonConvert.DeserializeObject<DeleteRoleRequest>(api.Message.Data)!.Name.Should().Be("operators");
    }

    [Fact]
    public async Task GetPermissionsCommand_builds_get_permissions_request()
    {
        var api = new CapturingCliApi(new GetPermissionsResponse { Success = true, Results = [new() { Name = "query:users" }] });
        var command = new GetPermissionsCommand(CreateServiceProvider(api));

        var exitCode = await command.HandleCommand(cancellationToken: CancellationToken.None);

        exitCode.Should().Be(1);
        api.Message!.RequestType.Should().Be(nameof(GetPermissionsRequest));
    }

    [Fact]
    public async Task GetRolesWhereCommand_builds_filtered_get_roles_request()
    {
        var api = new CapturingCliApi(new GetRolesResponse { Success = true, Results = [new() { Name = "operators" }] });
        var command = new GetRolesWhereCommand(CreateServiceProvider(api));

        var exitCode = await command.HandleCommand("x.Name = 'operators'", cancellationToken: CancellationToken.None);

        exitCode.Should().Be(1);
        api.Message!.RequestType.Should().Be(nameof(GetRolesRequest));
        JsonConvert.DeserializeObject<GetRolesRequest>(api.Message.Data)!.Where.Should().Be("x.Name = 'operators'");
    }

    private static ServiceProvider CreateServiceProvider(ICLIApi api)
    {
        return new ServiceCollection()
            .AddSingleton(api)
            .BuildServiceProvider();
    }

    private sealed class CapturingCliApi : ICLIApi
    {
        private readonly object _response;

        public CapturingCliApi(object response)
        {
            _response = response;
        }

        public SerializedRequest? Message { get; private set; }

        public Task<T> PostAdminMessage<T>(SerializedRequest message, CancellationToken cancellationToken = default)
        {
            Message = message;
            return Task.FromResult((T)_response);
        }
    }
}
