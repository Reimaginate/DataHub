using System.Diagnostics;
using Xunit.Abstractions;

namespace Reimaginate.DataHub.TestFramework.XUnit.Helpers;

public static class DiagnosticsExtensions
{
    public static Activity? WithCorrelationId(this Activity? activity, ITestOutputHelper testOutputHelper)
    {
        var correlationId = Guid.NewGuid().ToString();
        testOutputHelper.WriteLine("Correlation Id: {0}", correlationId);
        activity?.SetTag("correlation_id", correlationId);
        return activity;
    }

    public static Activity? IncludeRequests(this Activity? activity)
    {
        var correlationId = Guid.NewGuid().ToString();
        activity?.SetTag("include_request", "true");
        return activity;
    }

    public static Activity? IncludeResponses(this Activity? activity)
    {
        var correlationId = Guid.NewGuid().ToString();
        activity?.SetTag("include_response", "true");
        return activity;
    }
}
