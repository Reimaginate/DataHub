using Reimaginate.Mediator;

namespace Reimaginate.DataHub;

public sealed class DataHubQueryParameter
{
    public string? Name { get; set; }
    public object? Value { get; set; }
}

public abstract class DataHubClientRequest<TResponse> : IRequest<TResponse>
{
    public string? CorrelationId { get; set; }
    public string? RequestType { get; set; }
    public IReadOnlyCollection<DataHubQueryParameter>? Parameters { get; set; }
    // Keep this untyped so Abstractions does not depend on SharedModels.
    public object? TraceOptions { get; set; }
}
