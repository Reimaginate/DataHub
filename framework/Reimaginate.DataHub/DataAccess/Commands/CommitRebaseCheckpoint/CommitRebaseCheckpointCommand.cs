using System;
using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.DataAccess.Commands.CommitRebaseCheckpoint;

public class CommitRebaseCheckpointCommand : IRequest<CommitRebaseCheckpointResponse>
{
    public ChangeTrackingEntry Baseline { get; set; }
    public List<ChangeTrackingEntry> EntriesToDelete { get; set; } = [];
    internal Action EnsureLease { get; set; }
}

public class CommitRebaseCheckpointResponse
{
    public bool Success { get; set; }
    public bool TooLarge { get; set; }
    public bool OutcomeUnknown { get; set; }
    public string FailureReason { get; set; }
}
