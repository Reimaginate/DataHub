using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Reimaginate.DataHub.Models;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataServices;
using Reimaginate.Mediator;
using CancellationToken = System.Threading.CancellationToken;

// ReSharper disable IdentifierTypo

namespace Reimaginate.DataHub.DataAccess.Commands.UpsertLogEntries;

public class UpsertLogEntriesCommandHandler(IServiceProvider serviceProvider) : IHandler<UpsertLogEntriesCommand, UpsertLogEntriesResponse>
{
    private readonly IPartitionedDataService<LogEntry> _dataService = serviceProvider.GetRequiredService<IPartitionedDataService<LogEntry>>();

    public async System.Threading.Tasks.Task<UpsertLogEntriesResponse> HandleAsync(UpsertLogEntriesCommand request, CancellationToken cancellationToken)
    {
        var results = await _dataService.BulkUpsertItemsAsync(request.LogEntries, cancellationToken);
        return new UpsertLogEntriesResponse()
        {
            Successes = results.Successes,
            Failures = results.Failures.Select(s => new DataAccessFailure<LogEntry>(s.Item, s.Error)).ToList()
        };
    }
}
