using System.Diagnostics;

namespace Reimaginate.DataHub.TestFramework.Agents.DataHub;

public static class DiagnosticConfig
{
    public static class DataHub
    {
        public const string ServiceName = "DataHub";
        public const string ApplicationName = "DataHub";
        public const string ApplicationVersion = "1.0.0";
        public static ActivitySource ActivitySource = new(ApplicationName, ApplicationVersion);
    }
}