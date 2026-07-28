namespace Reimaginate.DataHub.SharedModels.Constants;

public static class PropertyMergeRuleActions
{
    public const string AlwaysOverwrite = "AlwaysOverwrite";
    public const string OverwriteIfNewer = "OverwriteIfNewer";
    public const string OverwriteIfEmpty = "OverwriteIfEmpty";
    public const string OverwriteIfNotEmpty = "OverwriteIfNotEmpty";
    public const string NeverOverwrite = "NeverOverwrite";
    public const string DoNotUpdate = "DoNotUpdate";
}