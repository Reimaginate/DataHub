using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;

// ReSharper disable InconsistentNaming
namespace Reimaginate.DataHub.Requests.External.Client.DeserializeClientRequest;

public sealed class DeserializeClientRequestRequest : IRequest<IRequest>
{
    public SerializedRequest SerializedRequest { get; set; }
}