using Microsoft.Extensions.DependencyInjection;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using Reimaginate.CLI.Base.Attributes;
using Reimaginate.DataHub.CLI.Tools.Shared.Helpers;
using Reimaginate.DataHub.CLI.Tools.Shared.Requests.Send;
using Spectre.Console;
using System.CommandLine.NamingConventionBinder;
using Newtonsoft.Json;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.Mediator;


namespace Reimaginate.DataHub.CLI.Tools.Commands.Sync;

[Argument("dataSource", required: true)]
[Argument("entityType", required: true)]
[Argument("entityIds", allowMultiple: true, required: true, type: typeof(string[]))]
[Option("additional-properties", required: false, aliases: "add-props,ap")]
[Option("info", typeof(bool), required: false, aliases: "i")]
[Option("properties", required: false, aliases: "props,p")]
public class SyncCommand : TopLevelCommand
{
    public SyncCommand(IServiceProvider serviceProvider) : base("sync", serviceProvider)
    {
        Handler = CommandHandler.Create(HandleCommand);
    }

    private readonly string _defaultProperties = string.Join(",", new List<string>()
    {
        nameof(SyncEntityResponse.DataSource),
        nameof(SyncEntityResponse.DataHubEntityType),
        nameof(SyncEntityResponse.DataHubEntityId),
        nameof(SyncEntityResponse.SourceEntityType),
        nameof(SyncEntityResponse.SourceEntityId),
        nameof(SyncEntityResponse.SyncOutcome)
    });

    public async Task<int> HandleCommand(string dataSource, string entityType, string[] entityIds, CancellationToken cancellationToken, string? properties = null, string? additionalProperties = null, bool info = false)
    {
        try
        {
            var mediator = ServiceProvider.GetRequiredService<IMediator>();
            var sendResponse = (await mediator.TrySend<SendResponse>(new SendRequest(new SyncEntitiesRequest()
            {
                DataSource = dataSource,
                DataHubEntityType = entityType,
                DataHubEntityIds = entityIds.ToList()
            }), cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var response } => response! };

            var syncEntitiesResponse = sendResponse.Result!.ToObject<SyncEntitiesResponse>()!;

            if (!string.IsNullOrEmpty(syncEntitiesResponse.JobId))
            {
                sendResponse = (await mediator.TrySend<SendResponse>(new SendRequest(new GetJobRequest()
                {
                    JobId = syncEntitiesResponse.JobId
                }), cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var response } => response! };

                var getJobResponse = sendResponse.Result!.ToObject<GetJobResponse>()!;
                var job = getJobResponse.Result!;

                AnsiConsole.WriteLine($"Job submitted successfully. The Job Id is {job.JobId} ");

                await CliProgressHelper.StatusAsync("Waiting for job completion", async () =>
                {
                    while (job.Status.ToLower() != "complete" && job.Status.ToLower() != "failed")
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);

                        sendResponse = (await mediator.TrySend<SendResponse>(new SendRequest(new GetJobRequest()
                        {
                            JobId = syncEntitiesResponse.JobId
                        }), cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var response } => response! };

                        getJobResponse = sendResponse.Result!.ToObject<GetJobResponse>()!;
                        job = getJobResponse.Result!;
                    }
                });

                if (job.Status.ToLower() == "failed")
                {
                    AnsiConsole.MarkupLine("[red]Job failed with response: [/]");
                    AnsiConsole.WriteLine(job.Response.ToString(Formatting.Indented));
                    return 0;
                }

                AnsiConsole.WriteLine("Job completed successfully");

            }

            return 1;
        }
        catch (TaskCanceledException)
        {
            AnsiConsole.WriteLine("Wait cancelled");
            return 1;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return 0;
        }
    }
}

