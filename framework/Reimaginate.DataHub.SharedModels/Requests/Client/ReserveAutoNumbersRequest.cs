namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class ReserveAutoNumbersRequest : DataHubClientRequest<ReserveAutoNumbersResponse>
{
    public ReserveAutoNumbersRequest()
    {
        RequestType = nameof(ReserveAutoNumbersRequest);
    }

    public string SequenceName { get; set; }
    public int Count { get; set; } = 1;
}
