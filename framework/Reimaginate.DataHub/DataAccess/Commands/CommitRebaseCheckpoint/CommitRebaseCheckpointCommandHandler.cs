using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Reimaginate.DataHub.Services.RebaseTracking;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.DataAccess.Commands.CommitRebaseCheckpoint;

public class CommitRebaseCheckpointCommandHandler(IServiceProvider services)
    : IHandler<CommitRebaseCheckpointCommand, CommitRebaseCheckpointResponse>
{
    public Task<CommitRebaseCheckpointResponse> HandleAsync(CommitRebaseCheckpointCommand request, CancellationToken cancellationToken)
        => services.GetRequiredService<IRebaseCheckpointStore>().CommitAsync(request, cancellationToken);
}
