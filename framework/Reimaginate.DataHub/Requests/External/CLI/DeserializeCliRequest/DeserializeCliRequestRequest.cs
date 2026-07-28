using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;

// ReSharper disable InconsistentNaming
namespace Reimaginate.DataHub.Requests.External.CLI.DeserializeCliRequest;

public sealed class DeserializeCliRequestRequest : IRequest<IRequest>
{
    public SerializedRequest SerializedRequest { get; set; }
}