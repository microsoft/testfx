// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.Metrics;

using Microsoft.Testing.Platform.Telemetry;

namespace Microsoft.Testing.Extensions.OpenTelemetry;

internal sealed class OpenTelemetryPlatformService : IPlatformOpenTelemetryService
{
    internal const string ActivitySourceName = "Microsoft.Testing.Platform";
    internal const string MeterName = "Microsoft.Testing.Platform";

    private readonly ActivitySource _activitySource = new(ActivitySourceName, PlatformVersion.Version);
    private readonly Meter _meter = new(MeterName, PlatformVersion.Version);
    private bool _isDisposed;

    public IActivity? TestFrameworkActivity { get; set; }

    public IActivity? StartActivity([CallerMemberName] string name = "", IEnumerable<KeyValuePair<string, object?>>? tags = null, string? parentId = null, DateTimeOffset startTime = default)
        => _activitySource.StartActivity(name, ActivityKind.Internal, tags: tags, startTime: startTime, parentId: parentId) is Activity activity
            ? new ActivityWrapper(activity)
            : null;

    public ICounter<T> CreateCounter<T>(string name, string? unit = null, string? description = null, IEnumerable<KeyValuePair<string, object?>>? tags = null)
        where T : struct
        => new CounterWrapper<T>(_meter.CreateCounter<T>(name, unit, description, tags!));

    public IHistogram<T> CreateHistogram<T>(string name, string? unit = null, string? description = null, IEnumerable<KeyValuePair<string, object?>>? tags = null)
        where T : struct
        => new HistogramWrapper<T>(_meter.CreateHistogram<T>(name, unit, description, tags));

    public void Dispose()
    {
#pragma warning disable CA1513 // Use ObjectDisposedException throw helper - not supported for netstandard2.0
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(OpenTelemetryPlatformService));
        }
#pragma warning restore CA1513 // Use ObjectDisposedException throw helper

        _activitySource.Dispose();
        _isDisposed = true;
    }
}
