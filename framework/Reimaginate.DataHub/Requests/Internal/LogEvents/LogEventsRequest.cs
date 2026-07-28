using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core.Models.Events;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.LogEvents;

public class LogEventsRequest<TEvent> : IRequest<LogEventsResponse> where TEvent : Event
{
    public List<TEvent> Events { get; set; }
}