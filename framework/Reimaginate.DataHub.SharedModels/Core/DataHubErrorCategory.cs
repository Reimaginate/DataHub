namespace Reimaginate.DataHub.SharedModels.Core;

public static class DataHubErrorCategory
{
    public const string InvalidRequest = nameof(InvalidRequest);
    public const string DeserializationFailed = nameof(DeserializationFailed);
    public const string ValidationFailed = nameof(ValidationFailed);
    public const string Unauthorized = nameof(Unauthorized);
    public const string ServerError = nameof(ServerError);
}
