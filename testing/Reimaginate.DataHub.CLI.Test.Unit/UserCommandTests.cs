using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Reimaginate.DataHub.CLI.Tools.Commands.Register.User;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Xunit;

namespace Reimaginate.DataHub.CLI.Test.Unit;

public class UserCommandTests
{
    [Fact]
    public async Task RegisterUserCommand_builds_user_request_with_entra_object_id()
    {
        var api = new CapturingCliApi();
        var serviceProvider = new ServiceCollection()
            .AddSingleton<ICLIApi>(api)
            .BuildServiceProvider();
        var command = new RegisterUserCommand(serviceProvider);

        var exitCode = await command.HandleCommand(
            "Admin User",
            "admin@contoso.test",
            "tenant-1",
            "object-1",
            "admin@contoso.test",
            "Admin,Reader",
            CancellationToken.None);

        exitCode.Should().Be(1);
        api.Message!.RequestType.Should().Be(nameof(RegisterUserRequest));
        var request = JsonConvert.DeserializeObject<RegisterUserRequest>(api.Message.Data)!;
        request.TenantId.Should().Be("tenant-1");
        request.EntraObjectId.Should().Be("object-1");
        request.UPN.Should().Be("admin@contoso.test");
        request.Roles.Should().Equal("admin", "reader");
    }

    private sealed class CapturingCliApi : ICLIApi
    {
        public SerializedRequest? Message { get; private set; }

        public Task<T> PostAdminMessage<T>(SerializedRequest message, CancellationToken cancellationToken = default)
        {
            Message = message;
            object response = new RegisterUserResponse { Success = true };
            return Task.FromResult((T)response);
        }
    }
}
