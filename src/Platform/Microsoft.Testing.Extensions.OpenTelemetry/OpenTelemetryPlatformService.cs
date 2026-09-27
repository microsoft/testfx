// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.Metrics;

using Microsoft.Testing.Platform.Telemetry;

namespace Microsoft.Testing.Extensions.OpenTelemetry;

internal sealed class OpenTelemetryPlatformService : IPlatformOpenTelemetryServiceWithActivityLinks
{
    internal const string ActivitySourceName = "Microsoft.Testing.Platform";
    internal const string MeterName = "Microsoft.Testing.Platform";

    private readonly ActivitySource _activitySource = new(ActivitySourceName, ExtensionVersion.DefaultSemVer);
    private readonly Meter _meter = new(MeterName, ExtensionVersion.DefaultSemVer);
    private readonly List<object> _observableInstruments = [];

    public IPlatformActivity? TestFrameworkActivity { get; set; }

    public string? RootTraceState { get; set; }

    public bool HasCurrentActivity => Activity.Current is not null;

    public IPlatformActivity? StartActivity([CallerMemberName] string name = "", IEnumerable<KeyValuePair<string, object?>>? tags = null, string? parentId = null, DateTimeOffset startTime = default)
        => StartActivityCore(name, tags, parentId, links: null, startTime) is Activity activity
            ? new ActivityWrapper(Stamp(activity))
            : null;

    public IPlatformActivity? StartNonAmbientActivity(string name, IEnumerable<KeyValuePair<string, object?>>? tags = null, string? parentId = null)
    {
        Activity? ambientBeforeStart = Activity.Current;
        if (StartActivityCore(name, tags, parentId, links: null, startTime: default) is not Activity activity)
        {
            return null;
        }

        // StartActivity unconditionally publishes the new activity as Activity.Current. Undo that immediately so
        // the span is timed and exported without ever leaking into an ExecutionContext captured by the code we wrap.
        Activity.Current = ambientBeforeStart;
        return new ActivityWrapper(Stamp(activity), isAmbient: false);
    }

    public PlatformActivityContext? CaptureCurrentActivityContext()
    {
        Activity? activity = Activity.Current;
        return activity is null
            || activity.IdFormat != ActivityIdFormat.W3C
            || activity.Id == TestFrameworkActivity?.Id
                ? null
                : new PlatformActivityContext(
                    activity.TraceId.ToHexString(),
                    activity.SpanId.ToHexString(),
                    activity.ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded),
                    activity.TraceStateString);
    }

    public IPlatformActivity? StartActivityWithLink(
        string name,
        IEnumerable<KeyValuePair<string, object?>>? tags,
        string? parentId,
        PlatformActivityContext linkContext)
    {
        ActivityContext activityContext = new(
            ActivityTraceId.CreateFromString(linkContext.TraceId.AsSpan()),
            ActivitySpanId.CreateFromString(linkContext.SpanId.AsSpan()),
            linkContext.IsRecorded ? ActivityTraceFlags.Recorded : ActivityTraceFlags.None,
            linkContext.TraceState,
            isRemote: false);

        return StartActivityCore(name, tags, parentId, [new ActivityLink(activityContext)], startTime: default) is Activity activity
            ? new ActivityWrapper(Stamp(activity))
            : null;
    }

    private Activity? StartActivityCore(
        string name,
        IEnumerable<KeyValuePair<string, object?>>? tags,
        string? parentId,
        IEnumerable<ActivityLink>? links,
        DateTimeOffset startTime)
        => parentId is null && Activity.Current is { IdFormat: ActivityIdFormat.W3C } ambientActivity
            ? _activitySource.StartActivity(name, ActivityKind.Internal, ambientActivity.Context, tags, links, startTime)
            : _activitySource.StartActivity(name, ActivityKind.Internal, tags: tags, links: links, startTime: startTime, parentId: parentId ?? Activity.Current?.Id);

    /// <summary>
    /// Activity only derives tracestate from an in-process parent reference, which an explicit parent id string
    /// does not provide. Stamp it on so the caller's vendor sampling state survives down to the test spans.
    /// </summary>
    private Activity Stamp(Activity activity)
    {
        if (RootTraceState is not null && activity.TraceStateString is null)
        {
            activity.TraceStateString = RootTraceState;
        }

        return activity;
    }

    public ICounter<T> CreateCounter<T>(string name, string? unit = null, string? description = null, IEnumerable<KeyValuePair<string, object?>>? tags = null)
        where T : struct
        => new CounterWrapper<T>(_meter.CreateCounter<T>(name, unit, description, tags));

    public IUpDownCounter<T> CreateUpDownCounter<T>(string name, string? unit = null, string? description = null, IEnumerable<KeyValuePair<string, object?>>? tags = null)
        where T : struct
        => new UpDownCounterWrapper<T>(_meter.CreateUpDownCounter<T>(name, unit, description, tags));

    public IHistogram<T> CreateHistogram<T>(string name, string? unit = null, string? description = null, IEnumerable<KeyValuePair<string, object?>>? tags = null)
        where T : struct
        => new HistogramWrapper<T>(_meter.CreateHistogram<T>(name, unit, description, tags));

    public void CreateObservableGauge<T>(string name, Func<T> observeValue, string? unit = null, string? description = null)
        where T : struct
    {
        // Observable instruments are polled by the metrics pipeline for as long as the meter is alive, so we keep
        // them rooted here and release them together with the meter.
        ObservableGauge<T> gauge = _meter.CreateObservableGauge(name, observeValue, unit, description);
        lock (_observableInstruments)
        {
            _observableInstruments.Add(gauge);
        }
    }

    public void Dispose()
    {
        // The Meter is intentionally not disposed: it is disposed of by the process exiting, and disposing it here
        // would remove its instruments before the MeterProvider (registered after this service, and therefore
        // disposed after it) gets a chance to flush the final measurements.
        _activitySource.Dispose();
        lock (_observableInstruments)
        {
            _observableInstruments.Clear();
        }
    }
}
