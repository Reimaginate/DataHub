using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using NSubstitute;
using Reimaginate.DataHub.Requests.External.Client.DeserializeClientRequest;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.TestFramework;

public static class DataHubClientMocks
{
    public static IDataHubClient InitDataHubClientMocks(IServiceProvider serviceProvider, IServiceProvider dataHubHostServices)
    {
        var dataHubClientMock = serviceProvider.GetRequiredService<IDataHubClient>();
        return InitDataHubClientMocks(dataHubClientMock, dataHubHostServices);
    }

    public static IDataHubClient InitDataHubClientMocks(IDataHubClient dataHubClientMock, IServiceProvider dataHubHostServices)
    {
        var sharedModelsAssembly = typeof(DataHubEntity).Assembly;
        var dataHubClientRequestTypes = sharedModelsAssembly.ExportedTypes.Where(type => (type.BaseType?.IsGenericType ?? false) && type.BaseType.GetGenericTypeDefinition() == typeof(DataHubClientRequest<>)).ToList();

        foreach (var dataHubClientRequestType in dataHubClientRequestTypes)
        {
            var responseType = dataHubClientRequestType.BaseType!.GetGenericArguments()[0];

            var postRequestMethod = typeof(IDataHubClient).GetMethod(nameof(IDataHubClient.PostRequestAsync));
            var genericPostRequestMethod = postRequestMethod!.MakeGenericMethod(dataHubClientRequestType, responseType);

            var t = genericPostRequestMethod.Invoke(dataHubClientMock, new object[] { default!, default! });

            t.ReturnsForAnyArgs(x =>
            {
                var request = x.Args()[0];

                var relayToDataHubMethod = typeof(DataHubClientMocks).GetMethod(nameof(RelayToDataHub));
                var genericRelayToDataHubMethod = relayToDataHubMethod!.MakeGenericMethod(responseType);

                var response = genericRelayToDataHubMethod.Invoke(null, new[] { dataHubHostServices, request });
                return response;
            });
        }

        return dataHubClientMock;
    }

    public static async Task<TResponse> RelayToDataHub<TResponse>(IServiceProvider dataHubHostServices, object payload)
    {
        var postMessage = new SerializedRequest()
        {
            RequestType = payload.GetType().Name,
            TraceOptions = payload.GetType().GetProperty(nameof(SerializedRequest.TraceOptions))?.GetValue(payload) as DataHubTraceOptions,
            Data = JsonConvert.SerializeObject(payload)
        };

        var dataHubMediator = dataHubHostServices.GetRequiredService<IMediator>();
        var convertPostMessageToIRequestResponse = await dataHubMediator.SendAsync(new DeserializeClientRequestRequest() { SerializedRequest = postMessage }, CancellationToken.None);
     
        var request = convertPostMessageToIRequestResponse.AsT0;

        var response = await dataHubMediator.SendAsync(request, CancellationToken.None);

        if (response.IsT1) throw response.AsT1;
        return (TResponse)response.AsT0;
    }

}
