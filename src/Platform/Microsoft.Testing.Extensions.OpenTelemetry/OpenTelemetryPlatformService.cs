// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.Metrics;

using Microsoft.Testing.Platform.Telemetry;

namespace Microsoft.Testing.Extensions.OpenTelemetry;

internal sealed class OpenTelemetryPlatformService : IPlatformOpenTelemetryService
{
    internal const string ActivitySourceName = "Microsoft.Testing.Platform";
    internal const string MeterName = "Microsoft.Testing.Platform";

    private readonly ActivitySource _activitySource = new(ActivitySourceName, ExtensionVersion.DefaultSemVer);
    private readonly Meter _meter = new(MeterName, ExtensionVersion.DefaultSemVer);
    private readonly List<object> _observableInstruments = [];

    public IPlatformActivity? TestFrameworkActivity { get; set; }

    public IPlatformActivity? CurrentActivity
        => Activity.Current is { } current ? new ActivityWrapper(current) : null;

    public IPlatformActivity? StartActivity([CallerMemberName] string name = "", IEnumerable<KeyValuePair<string, object?>>? tags = null, string? parentId = null, DateTimeOffset startTime = default, PlatformActivityKind kind = PlatformActivityKind.Internal)
        => _activitySource.StartActivity(name, ToActivityKind(kind), tags: tags, startTime: startTime, parentId: parentId) is Activity activity
            ? new ActivityWrapper(activity)
            : null;

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
        _activitySource.Dispose();
        _meter.Dispose();
        lock (_observableInstruments)
        {
            _observableInstruments.Clear();
        }
    }

    private static ActivityKind ToActivityKind(PlatformActivityKind kind)
        => kind switch
        {
            PlatformActivityKind.Server => ActivityKind.Server,
            PlatformActivityKind.Client => ActivityKind.Client,
            PlatformActivityKind.Producer => ActivityKind.Producer,
            PlatformActivityKind.Consumer => ActivityKind.Consumer,
            _ => ActivityKind.Internal,
        };
}
