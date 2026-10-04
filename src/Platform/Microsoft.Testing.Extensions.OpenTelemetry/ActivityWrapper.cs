// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Telemetry;

namespace Microsoft.Testing.Extensions.OpenTelemetry;

internal sealed class ActivityWrapper(Activity activity) : IPlatformTestExecutionActivity
{
    private const string ExceptionEventName = "exception";
    private const string ExceptionTypeTag = "exception.type";
    private const string ExceptionMessageTag = "exception.message";
    private const string ExceptionStackTraceTag = "exception.stacktrace";

    public string? Id => activity.Id;

    public string? TraceId => activity.IdFormat == ActivityIdFormat.W3C ? activity.TraceId.ToHexString() : null;

    public string? SpanId => activity.IdFormat == ActivityIdFormat.W3C ? activity.SpanId.ToHexString() : null;

    public bool IsRecording => activity.IsAllDataRequested;

    public IPlatformActivity SetTag(string key, object? value)
    {
        activity.SetTag(key, value);
        return this;
    }

    public IPlatformActivity SetStatus(PlatformActivityStatusCode statusCode, string? description = null)
    {
        activity.SetStatus(
            statusCode switch
            {
                PlatformActivityStatusCode.Ok => ActivityStatusCode.Ok,
                PlatformActivityStatusCode.Error => ActivityStatusCode.Error,
                _ => ActivityStatusCode.Unset,
            },
            description);
        return this;
    }

    public IPlatformActivity AddEvent(string name, IEnumerable<KeyValuePair<string, object?>>? tags = null, DateTimeOffset timestamp = default)
    {
        ActivityTagsCollection? tagsCollection = null;
        if (tags is not null)
        {
            tagsCollection = [];
            foreach (KeyValuePair<string, object?> tag in tags)
            {
                tagsCollection[tag.Key] = tag.Value;
            }
        }

        activity.AddEvent(new ActivityEvent(name, timestamp, tagsCollection));
        return this;
    }

    public IPlatformActivity RecordException(Exception exception, IEnumerable<KeyValuePair<string, object?>>? additionalTags = null)
    {
        // exception.escaped is deliberately omitted: it is deprecated upstream, and a test failure is reported
        // through the result attributes rather than by letting the exception escape the span.
        ActivityTagsCollection tags = new()
        {
            [ExceptionTypeTag] = exception.GetType().FullName,
            [ExceptionMessageTag] = exception.Message,
            [ExceptionStackTraceTag] = exception.ToString(),
        };

        if (additionalTags is not null)
        {
            foreach (KeyValuePair<string, object?> tag in additionalTags)
            {
                tags[tag.Key] = tag.Value;
            }
        }

        activity.AddEvent(new ActivityEvent(ExceptionEventName, tags: tags));
        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        return this;
    }

    public IDisposable Enter()
        => new ActivityScope(activity);

    public void Stop(DateTimeOffset endTime)
    {
        DateTime endUtc = endTime.UtcDateTime;
        if (endUtc <= activity.StartTimeUtc)
        {
            endUtc = activity.StartTimeUtc.AddTicks(1);
        }

        Activity? current = Activity.Current;
        activity.SetEndTime(endUtc);
        activity.Stop();
        Activity.Current = current;
    }

    public void Dispose()
    {
        Activity? current = Activity.Current;
        activity.Dispose();
        if (current != activity)
        {
            // The wrapped activity was non-ambient. Activity.Stop() can reassign Activity.Current to its parent,
            // so restore the activity that was actually ambient before disposal.
            Activity.Current = current;
        }
    }

    private sealed class ActivityScope : IDisposable
    {
        private readonly Activity? _previous = Activity.Current;
        private bool _disposed;

        public ActivityScope(Activity activity)
            => Activity.Current = activity;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Activity.Current = _previous;
        }
    }
}
