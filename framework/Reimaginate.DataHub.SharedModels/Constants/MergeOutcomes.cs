namespace Reimaginate.DataHub.SharedModels.Constants;

public static class MergeOutcomes
{
    public const string SourceEntityNotFound = "Source Entity Not Found";
    public const string EntityMatchedAndUpdated = "Entity Matched And Updated";
    public const string EntityMatchedButNotUpdated = "Entity Matched But Not Updated";
    public const string MergeFailed = "Merge Failed";
    public const string MergeRejected = "Merge Rejected";
    public const string MergeSilentlyRejected = "Merge Silently Rejected";
    public const string NewEntityCreated = "New Entity Created";
    public const string NoSourceEntityUpdateToProcess = "No Source Entity Update To Process";

    public static bool IsFailure(string input)
    {
        return input == MergeFailed || input == SourceEntityNotFound || input == MergeRejected;
    }

    public static bool IsSuccess(string input)
    {
        return input == EntityMatchedAndUpdated || input == EntityMatchedButNotUpdated || input == MergeSilentlyRejected || input == NewEntityCreated || input == NoSourceEntityUpdateToProcess;
    }
}
