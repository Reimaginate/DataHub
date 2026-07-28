using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Exceptions;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.External.Client.DeserializeClientRequest;

public class DeserializeClientRequestRequestHandler : IHandler<DeserializeClientRequestRequest, IRequest>
{
    public Task<IRequest> HandleAsync(DeserializeClientRequestRequest request, CancellationToken cancellationToken)
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

        if (string.IsNullOrWhiteSpace(serializedRequest.Data))
        {
            throw new DataHubInvalidRequestException(
                "Request Data is required.",
                serializedRequest.RequestType,
                serializedRequest.CorrelationId);
        }

        var tname = $"Reimaginate.DataHub.SharedModels.Requests.Client.{serializedRequest.RequestType}";
        var aqname = Assembly.CreateQualifiedName(typeof(DataHubEntity).Assembly.FullName, tname);

        var requestType = Type.GetType(aqname);
        if (requestType == null)
        {
            throw new DataHubInvalidRequestException(
                $"Invalid request type '{serializedRequest.RequestType}'.",
                serializedRequest.RequestType,
                serializedRequest.CorrelationId);
        }

        try
        {
            var ret = JsonConvert.DeserializeObject(serializedRequest.Data, requestType, new JsonSerializerSettings
            {
                DateParseHandling = DateParseHandling.DateTimeOffset,
                MissingMemberHandling = MissingMemberHandling.Ignore
            });

            if (ret == null)
            {
                throw new DataHubDeserializationException(
                    "Request Data could not be deserialized.",
                    serializedRequest.RequestType,
                    serializedRequest.CorrelationId);
            }

            ApplyEnvelopeMetadata(ret, serializedRequest);
            return Task.FromResult((IRequest)ret);
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
    }

    private static void ApplyEnvelopeMetadata(object deserializedRequest, SerializedRequest serializedRequest)
    {
        ApplyCorrelationId(deserializedRequest, serializedRequest.CorrelationId);
        ApplyTraceOptions(deserializedRequest, serializedRequest.TraceOptions);
    }

    private static void ApplyCorrelationId(object deserializedRequest, string correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            return;
        }

        var correlationProperty = deserializedRequest.GetType().GetProperty(nameof(SerializedRequest.CorrelationId));
        if (correlationProperty?.CanWrite != true || correlationProperty.PropertyType != typeof(string))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace((string)correlationProperty.GetValue(deserializedRequest)))
        {
            correlationProperty.SetValue(deserializedRequest, correlationId);
        }
    }

    private static void ApplyTraceOptions(object deserializedRequest, DataHubTraceOptions traceOptions)
    {
        if (traceOptions == null)
        {
            return;
        }

        var traceOptionsProperty = deserializedRequest.GetType().GetProperty(nameof(SerializedRequest.TraceOptions));
        if (traceOptionsProperty?.CanWrite == true &&
            (traceOptionsProperty.PropertyType == typeof(DataHubTraceOptions) ||
             traceOptionsProperty.PropertyType == typeof(object)))
        {
            traceOptionsProperty.SetValue(deserializedRequest, traceOptions);
        }
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
