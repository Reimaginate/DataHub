using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Reimaginate.DataHub.AspNetCore.Observability;
using Reimaginate.DataHub.Requests.External.CLI.DeserializeCliRequest;
using Reimaginate.DataHub.Requests.External.Client.DeserializeClientRequest;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Exceptions;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.AspNetCore;

public static class DataHubEndpointRouteBuilderExtensions
{
    public static RouteHandlerBuilder MapDataHubClientEndpoint(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Action<DataHubClientEndpointOptions> configure = null)
    {
        var options = new DataHubClientEndpointOptions();
        configure?.Invoke(options);

        return endpoints.MapPost(pattern, async (
            HttpContext httpContext,
            CancellationToken cancellationToken,
            IMediator mediator,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("Reimaginate.DataHub.AspNetCore.ClientEndpoint");
            var observabilityOptions = httpContext.RequestServices.GetService<IOptions<DataHubObservabilityOptions>>()?.Value ?? new DataHubObservabilityOptions();
            SerializedRequest serializedRequest = null;

            try
            {
                if (options.AuthorizeAsync != null && !await options.AuthorizeAsync(httpContext.Request, cancellationToken))
                {
                    return DataHubEndpointDiagnostics.CreateUnauthorizedResult(httpContext, logger);
                }

                serializedRequest = await ReadSerializedRequestAsync(httpContext.Request, cancellationToken);
                DataHubEndpointObservability.ApplyRequest(serializedRequest, observabilityOptions, "client");
                var request = (await mediator.TrySend<IRequest>(
                    new DeserializeClientRequestRequest { SerializedRequest = serializedRequest },
                    cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

                var response = (await mediator.SendAsync(request, cancellationToken)) switch { { IsT1: true } result => throw result.AsT1, { AsT0: var mediatorResultValue } => mediatorResultValue };
                DataHubEndpointObservability.ApplyResponse(response, observabilityOptions);
                return Results.Text(JsonConvert.SerializeObject(response), "application/json", Encoding.UTF8);
            }
            catch (Exception ex)
            {
                return DataHubEndpointDiagnostics.CreateErrorResult(
                    ex,
                    httpContext,
                    logger,
                    serializedRequest?.RequestType,
                    serializedRequest?.CorrelationId);
            }
        });
    }

    public static RouteHandlerBuilder MapDataHubCliEndpoint(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Action<DataHubCliEndpointOptions> configure)
    {
        var options = new DataHubCliEndpointOptions();
        configure?.Invoke(options);

        return endpoints.MapPost(pattern, async (
            HttpContext httpContext,
            CancellationToken cancellationToken,
            IMediator mediator,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("Reimaginate.DataHub.AspNetCore.CliEndpoint");
            var observabilityOptions = httpContext.RequestServices.GetService<IOptions<DataHubObservabilityOptions>>()?.Value ?? new DataHubObservabilityOptions();
            SerializedRequest serializedRequest = null;

            try
            {
                if (options.AuthenticateAsync == null)
                {
                    throw new InvalidOperationException("CLI endpoint authentication has not been configured.");
                }

                var user = await options.AuthenticateAsync(httpContext.Request, cancellationToken);
                if (user == null)
                {
                    return DataHubEndpointDiagnostics.CreateUnauthorizedResult(httpContext, logger);
                }

                serializedRequest = await ReadSerializedRequestAsync(httpContext.Request, cancellationToken);
                DataHubEndpointObservability.ApplyRequest(serializedRequest, observabilityOptions, "cli");
                var cliRequest = (DataHubCLIRequest)((await mediator.TrySend<IRequest>(
                    new DeserializeCliRequestRequest { SerializedRequest = serializedRequest },
                    cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue });
                cliRequest.User = user;

                var response = (await mediator.SendAsync(cliRequest, cancellationToken)) switch { { IsT1: true } result => throw result.AsT1, { AsT0: var mediatorResultValue } => mediatorResultValue };
                DataHubEndpointObservability.ApplyResponse(response, observabilityOptions);
                return Results.Text(JsonConvert.SerializeObject(response), "application/json", Encoding.UTF8);
            }
            catch (Exception ex)
            {
                return DataHubEndpointDiagnostics.CreateErrorResult(
                    ex,
                    httpContext,
                    logger,
                    serializedRequest?.RequestType,
                    serializedRequest?.CorrelationId);
            }
        });
    }

    private static async Task<SerializedRequest> ReadSerializedRequestAsync(HttpRequest httpRequest, CancellationToken cancellationToken)
    {
        var requestBody = await new StreamReader(httpRequest.Body).ReadToEndAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(requestBody))
        {
            throw new DataHubInvalidRequestException(
                "Request body is required.",
                correlationId: DataHubEndpointDiagnostics.ResolveCorrelationId(httpRequest.HttpContext));
        }

        try
        {
            var serializedRequest = JsonConvert.DeserializeObject<SerializedRequest>(requestBody);
            if (serializedRequest == null)
            {
                throw new DataHubInvalidRequestException(
                    "Request envelope is required.",
                    correlationId: DataHubEndpointDiagnostics.ResolveCorrelationId(httpRequest.HttpContext));
            }

            if (string.IsNullOrWhiteSpace(serializedRequest.CorrelationId))
            {
                serializedRequest.CorrelationId = DataHubEndpointDiagnostics.ResolveCorrelationId(httpRequest.HttpContext);
            }

            return serializedRequest;
        }
        catch (JsonException ex)
        {
            throw new DataHubDeserializationException(
                "Request envelope could not be deserialized.",
                correlationId: DataHubEndpointDiagnostics.ResolveCorrelationId(httpRequest.HttpContext),
                details: new[]
                {
                    new DataHubErrorDetail
                    {
                        Field = ex is JsonReaderException readerException ? readerException.Path : null,
                        Code = ex.GetType().Name,
                        Message = ex.Message
                    }
                },
                innerException: ex);
        }
    }
}
