using Reimaginate.DataHub.Requests.Internal.GetLogs;
using Reimaginate.DataHub.Requests.Internal.LogEvents;
using Reimaginate.DataHub.Requests.Internal.LogSyncEvents;
using Reimaginate.DataHub.Requests.Internal.RecordSyncEventsAgainstDataHubEntities;
using Reimaginate.DataHub.SharedModels.Core.Models.Events;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub;

[Mediator]
[ScanAssembly(typeof(Mediator))]
[AddHandler(typeof(GetLogsRequest<MergeFailure>), typeof(GetLogsRequestHandler<MergeFailure>))]
[AddHandler(typeof(GetLogsRequest<MergeSuccess>), typeof(GetLogsRequestHandler<MergeSuccess>))]
[AddHandler(typeof(GetLogsRequest<SyncFailure>), typeof(GetLogsRequestHandler<SyncFailure>))]
[AddHandler(typeof(GetLogsRequest<SyncSuccess>), typeof(GetLogsRequestHandler<SyncSuccess>))]
[AddHandler(typeof(LogEventsRequest<Alert>), typeof(LogEventsRequestHandler<Alert>))]
[AddHandler(typeof(LogEventsRequest<CustomEvent>), typeof(LogEventsRequestHandler<CustomEvent>))]
[AddHandler(typeof(LogEventsRequest<PatchFailure>), typeof(LogEventsRequestHandler<PatchFailure>))]
[AddHandler(typeof(LogEventsRequest<MergeFailure>), typeof(LogEventsRequestHandler<MergeFailure>))]
[AddHandler(typeof(LogEventsRequest<MergeSuccess>), typeof(LogEventsRequestHandler<MergeSuccess>))]
[AddHandler(typeof(LogEventsRequest<SyncFailure>), typeof(LogEventsRequestHandler<SyncFailure>))]
[AddHandler(typeof(LogEventsRequest<SyncSuccess>), typeof(LogEventsRequestHandler<SyncSuccess>))]
[AddHandler(typeof(LogSyncEventsRequest<MergeFailure>), typeof(LogSyncEventsRequestHandler<MergeFailure>))]
[AddHandler(typeof(LogSyncEventsRequest<MergeSuccess>), typeof(LogSyncEventsRequestHandler<MergeSuccess>))]
[AddHandler(typeof(LogSyncEventsRequest<SyncFailure>), typeof(LogSyncEventsRequestHandler<SyncFailure>))]
[AddHandler(typeof(LogSyncEventsRequest<SyncSuccess>), typeof(LogSyncEventsRequestHandler<SyncSuccess>))]
[AddHandler(typeof(RecordSyncEventsAgainstDataHubEntitiesRequest<MergeFailure>), typeof(RecordSyncEventsAgainstDataHubEntitiesRequestHandler<MergeFailure>))]
[AddHandler(typeof(RecordSyncEventsAgainstDataHubEntitiesRequest<MergeSuccess>), typeof(RecordSyncEventsAgainstDataHubEntitiesRequestHandler<MergeSuccess>))]
[AddHandler(typeof(RecordSyncEventsAgainstDataHubEntitiesRequest<SyncFailure>), typeof(RecordSyncEventsAgainstDataHubEntitiesRequestHandler<SyncFailure>))]
[AddHandler(typeof(RecordSyncEventsAgainstDataHubEntitiesRequest<SyncSuccess>), typeof(RecordSyncEventsAgainstDataHubEntitiesRequestHandler<SyncSuccess>))]
public partial class Mediator
{
}
