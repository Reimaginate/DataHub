using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.CLI.Tools.Shared.Requests.Send;

public class SendRequest(DataHubCLIRequest request) : IRequest<SendResponse>
{
    public DataHubCLIRequest Request { get; set; } = request;
}

public class SendRequestHandler(ICLIApi cliApp) : IHandler<SendRequest, SendResponse>
{
    public async Task<SendResponse> HandleAsync(SendRequest request, CancellationToken cancellationToken)
    {
        var response = await cliApp.PostAdminMessage<JToken>(new SerializedRequest()
        {
            RequestType = request.Request!.RequestType,
            CorrelationId = request.Request.CorrelationId,
            Data = JsonConvert.SerializeObject(request.Request)
        }, cancellationToken);

        return new SendResponse()
        {
            Result = response
        };
    }
}

public class SendResponse
{
    public JToken Result { get; set; } = JValue.CreateNull();
}
