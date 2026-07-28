namespace Reimaginate.DataHub.SharedModels.Constants;

public static class SyncOutcomes
{
    public const string DataHubEntityNotFound = "ResultingEntity Hub Entity Not Found";
    public const string SourceEntityUpdated = "Source Entity Updated";
    public const string SourceEntityMatchedButNotUpdated = "Source Entity Matched But Not Updated";
    public const string SyncFailed = "Sync Failed";
    public const string NewSourceEntityCreated = "New Source Entity Created";
    public const string NoSourceEntityUpdateToProcess = "No Source Entity Update To Process";

    public static bool IsFailure(string input)
    {
        return input == DataHubEntityNotFound || input == SyncFailed;
    }

    public static bool IsSuccess(string input)
    {
        return input == SourceEntityUpdated || input == SourceEntityMatchedButNotUpdated || input == NewSourceEntityCreated || input == NoSourceEntityUpdateToProcess;
    }

}
