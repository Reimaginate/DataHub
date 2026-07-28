namespace Reimaginate.DataHub.CLI.Tools.Shared.Runtime;

public static class CliExitCodes
{
    public const int Success = 0;
    public const int Failure = 1;
    public const int Usage = 2;
    public const int Cancelled = 130;
    internal const int LegacyRuntimeSuccess = 1;
    internal const int LegacyRuntimeFailure = 101;
}
