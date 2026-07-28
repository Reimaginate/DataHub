using System.Diagnostics;
using System.Diagnostics.Metrics;
using Reimaginate.DataHub.Diagnostics;

namespace Reimaginate.DataHub.Test.Unit.Tests;

internal sealed class ActivityCapture : IDisposable
{
    private readonly ActivityListener _listener;
    private readonly ActivitySource _source = new("Reimaginate.DataHub.Test.Unit");

    public ActivityCapture()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => StoppedActivities.Add(activity)
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public List<Activity> StoppedActivities { get; } = [];

    public Activity StartActivity(string name = "test")
    {
        return _source.StartActivity(name) ?? throw new InvalidOperationException("Test activity could not be started.");
    }

    public void Dispose()
    {
        _listener.Dispose();
        _source.Dispose();
    }
}

internal sealed class MetricCapture : IDisposable
{
    private readonly HashSet<string> _instrumentNames;
    private readonly MeterListener _listener;

    public MetricCapture(params string[] instrumentNames)
    {
        _instrumentNames = instrumentNames.Length == 0 ? [] : instrumentNames.ToHashSet(StringComparer.Ordinal);
        _listener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == DataHubTelemetry.MeterName &&
                    (_instrumentNames.Count == 0 || _instrumentNames.Contains(instrument.Name)))
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            }
        };
        _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            Measurements.Add(new CapturedMetric(
                instrument.Name,
                value,
                tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value, StringComparer.Ordinal)));
        });
        _listener.Start();
    }

    public List<CapturedMetric> Measurements { get; } = [];

    public void Dispose()
    {
        _listener.Dispose();
    }
}

internal sealed record CapturedMetric(string Name, long Value, Dictionary<string, object?> Tags);
