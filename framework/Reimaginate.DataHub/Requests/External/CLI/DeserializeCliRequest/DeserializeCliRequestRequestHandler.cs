using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Exceptions;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.CLI.DeserializeCliRequest;

public class DeserializeCliRequestRequestHandler : IHandler<DeserializeCliRequestRequest, IRequest>
{
    public Task<IRequest> HandleAsync(DeserializeCliRequestRequest request, CancellationToken cancellationToken)
    {
        var serializedRequest = request?.SerializedRequest;
        if (serializedRequest == null)
        {
            throw new DataHubInvalidRequestException("Request envelope is required.");
        }

        if (string.IsNullOrWhiteSpace(serializedRequest.RequestType))
        {
            throw new DataHubInvalidRequestException("RequestType is required.", correlationId: serializedRequest.CorrelationId);
        }

        var tname = $"Reimaginate.DataHub.SharedModels.Requests.CLI.{serializedRequest.RequestType}";
        var aqname = Assembly.CreateQualifiedName(typeof(DataHubCLIRequest).Assembly.FullName, tname);

        var requestType = Type.GetType(aqname);
        if (requestType == null)
        {
            throw new DataHubInvalidRequestException(
                $"Invalid request type '{serializedRequest.RequestType}'.",
                serializedRequest.RequestType,
                serializedRequest.CorrelationId);
        }

        object ret;
        try
        {
            ret = !string.IsNullOrEmpty(serializedRequest.Data)
                ? JsonConvert.DeserializeObject(serializedRequest.Data, requestType, new JsonSerializerSettings { DateParseHandling = DateParseHandling.DateTimeOffset })
                : Activator.CreateInstance(requestType);
        }
        catch (JsonException ex)
        {
            throw new DataHubDeserializationException(
                "Request Data could not be deserialized.",
                serializedRequest.RequestType,
                serializedRequest.CorrelationId,
                CreateJsonDetails(ex),
                ex);
        }

        if (ret == null)
        {
            throw new DataHubDeserializationException(
                "Request Data could not be deserialized.",
                serializedRequest.RequestType,
                serializedRequest.CorrelationId);
        }

        if (ret is DataHubCLIRequest cliRequest)
        {
            if (string.IsNullOrWhiteSpace(cliRequest.CorrelationId))
            {
                cliRequest.CorrelationId = serializedRequest.CorrelationId;
            }

            if (serializedRequest.TraceOptions != null)
            {
                cliRequest.TraceOptions = serializedRequest.TraceOptions;
            }
        }

        return Task.FromResult((IRequest)ret);
    }

    private static IReadOnlyCollection<DataHubErrorDetail> CreateJsonDetails(JsonException exception)
    {
        var path = exception switch
        {
            JsonReaderException readerException => readerException.Path,
            JsonSerializationException serializationException => serializationException.Path,
            _ => null
        };

        return new[]
        {
            new DataHubErrorDetail
            {
                Field = path,
                Code = exception.GetType().Name,
                Message = exception.Message
            }
        };
    }
}
