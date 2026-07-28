using System.CommandLine.NamingConventionBinder;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using Reimaginate.CLI.Base.Attributes;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Spectre.Console;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Update.User;

[Argument("id", typeof(string), required: true, description: "DataHub id of the user")]
[Option("name", typeof(string), required: true, description: "Name of the user")]
[Option("email", typeof(string), required: true, description: "Email address of the user")]
[Option("tenant-id", typeof(string), required: true, description: "The Microsoft Entra tenant id of the user")]
[Option("object-id", typeof(string), required: true, description: "The Microsoft Entra object id of the user")]
[Option("upn", typeof(string), required: false, description: "The Azure Tenant UPN of the user (if different from email")]
[Option("roles", typeof(string), required: true, description: "Comma delimited list of roles: Admin | Reader")]
public class UpdateUserCommand : SubCommand<UpdateCommand>
{
    public UpdateUserCommand(IServiceProvider serviceProvider) : base("user", serviceProvider)
    {
        Handler = CommandHandler.Create(HandleCommand);
    }
    
    public async Task<int> HandleCommand(string id, string name, string email, string tenantId, string objectId, string? upn, string roles, CancellationToken cancellationToken = default)
    {
        var adminApi = ServiceProvider.GetRequiredService<ICLIApi>();

        var response = await adminApi.PostAdminMessage<UpdateUserResponse>(new SerializedRequest()
        {
            RequestType = nameof(UpdateUserRequest),
            Data = JsonConvert.SerializeObject(new UpdateUserRequest()
            {
                Id = id,
                Name = name,
                Email = email,
                UPN = upn ?? email,
                TenantId = tenantId,
                EntraObjectId = objectId,
                Roles = roles?.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s=>s.Trim().ToLower()).ToList()
            })
        }, cancellationToken);

        if (!response.Success)
        {
            AnsiConsole.WriteLine("Failed to update user: " + response.FailureReason);
            return 0;
        }

        AnsiConsole.WriteLine("Update user succeeded");
        return 1;
    }
}

