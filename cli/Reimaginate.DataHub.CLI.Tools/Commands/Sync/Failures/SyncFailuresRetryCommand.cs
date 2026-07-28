using System.CommandLine.NamingConventionBinder;
using Microsoft.Extensions.DependencyInjection;
using Reimaginate.CLI.Base.Attributes;
using Reimaginate.DataHub.CLI.Tools.Commands.Get.Sync.Failures;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using Reimaginate.DataHub.CLI.Tools.Shared.API;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Sync.Failures;

[Argument("where", required: false)]
public class SyncFailuresRetryCommand : SubCommand<SyncFailuresCommand>
{
    public SyncFailuresRetryCommand(IServiceProvider serviceProvider) : base("retry", "Retry sync failures", serviceProvider)
    {
        Handler = CommandHandler.Create(HandleCommand);
    }

    public async Task<int> HandleCommand(string? where = null, CancellationToken cancellationToken = default)
    {
        var adminApi = ServiceProvider.GetRequiredService<ICLIApi>();
        return await GetSyncFailureHelpers.Retry(adminApi, where, cancellationToken, ServiceProvider);
    }
}
