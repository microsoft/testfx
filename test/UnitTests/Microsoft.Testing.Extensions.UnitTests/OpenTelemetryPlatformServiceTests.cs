// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

using Microsoft.Testing.Extensions.OpenTelemetry;
using Microsoft.Testing.Platform.Telemetry;

namespace Microsoft.Testing.Extensions.UnitTests;

/// <summary>
/// Exercises the real <see cref="OpenTelemetryPlatformService"/> against a live <see cref="ActivityListener"/>,
/// which is the only way to cover the ambient-context behaviour that the mock-based platform tests cannot see.
/// </summary>
/// <remarks>
/// <see cref="Activity.Current"/> and the listener registry are process-global and the test host may already have
/// an ambient activity of its own, so every assertion is made relative to the activity that was current when the
/// test started, and the listener only collects activities this instance created (identified by a unique name
/// prefix).
/// </remarks>
[TestClass]
public sealed class OpenTelemetryPlatformServiceTests : IDisposable
{
    private readonly string _namePrefix = $"test-{Guid.NewGuid():N}-";
    private readonly List<Activity> _stoppedActivities = [];
    private readonly Activity? _ambientAtStart = Activity.Current;
    private readonly ActivityListener _listener;
    private readonly OpenTelemetryPlatformService _service = new();

    public OpenTelemetryPlatformServiceTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == OpenTelemetryPlatformService.ActivitySourceName,
            Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (activity.OperationName.StartsWith(_namePrefix, StringComparison.Ordinal))
                {
                    lock (_stoppedActivities)
                    {
                        _stoppedActivities.Add(activity);
                    }
                }
            },
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose()
    {
        _listener.Dispose();
        _service.Dispose();
    }

    [TestMethod]
    public void StartActivity_ByDefault_PublishesTheActivityAsCurrent()
    {
        using (_service.StartActivity(Name("ambient")))
        {
            Assert.IsNotNull(Activity.Current);
            Assert.AreEqual(Name("ambient"), Activity.Current.OperationName);
            Assert.IsTrue(_service.HasCurrentActivity);
        }

        Assert.AreSame(_ambientAtStart, Activity.Current);
    }

    [TestMethod]
    public void StartNonAmbientActivity_NeverBecomesCurrent()
    {
        // This is what keeps MSTest fixture spans out of the ExecutionContext that MSTest captures inside a
        // fixture and replays for the rest of the run.
        using (_service.StartNonAmbientActivity(Name("non-ambient")))
        {
            Assert.AreSame(_ambientAtStart, Activity.Current);
        }

        Assert.AreSame(_ambientAtStart, Activity.Current);

        // The span is still timed and exported even though it was never current.
        Assert.AreEqual(Name("non-ambient"), Single().OperationName);
    }

    [TestMethod]
    public void StartNonAmbientActivity_DoesNotDisturbAnExistingAmbientActivity()
    {
        using (_service.StartActivity(Name("outer")))
        {
            Activity? ambient = Activity.Current;
            Assert.IsNotNull(ambient);

            using (_service.StartNonAmbientActivity(Name("inner")))
            {
                Assert.AreSame(ambient, Activity.Current);
            }

            // Stopping a non-ambient activity must not pop the ambient one.
            Assert.AreSame(ambient, Activity.Current);
        }

        Assert.AreSame(_ambientAtStart, Activity.Current);
    }

    [TestMethod]
    public void StartNonAmbientActivity_WithExplicitParentId_KeepsTheSameTrace()
    {
        using IPlatformActivity? parent = _service.StartActivity(Name("parent"));
        Assert.IsNotNull(parent);
        Assert.IsNotNull(parent.Id);

        using IPlatformActivity? child = _service.StartNonAmbientActivity(Name("child"), parentId: parent.Id);
        Assert.IsNotNull(child);
        Assert.AreEqual(parent.TraceId, child.TraceId);
        Assert.AreNotEqual(parent.SpanId, child.SpanId);
    }

    [TestMethod]
    public void SetStatus_MapsOntoActivityStatusCode()
    {
        using (IPlatformActivity? activity = _service.StartActivity(Name("status")))
        {
            Assert.IsNotNull(activity);
            activity.SetStatus(PlatformActivityStatusCode.Error, "it broke");
        }

        Activity stopped = Single();
        Assert.AreEqual(ActivityStatusCode.Error, stopped.Status);
        Assert.AreEqual("it broke", stopped.StatusDescription);
    }

    [TestMethod]
    public void RecordException_AddsTheConventionalExceptionEventAndFailsTheSpan()
    {
        InvalidOperationException exception = new("boom");

        using (IPlatformActivity? activity = _service.StartActivity(Name("exception")))
        {
            Assert.IsNotNull(activity);
            activity.RecordException(exception);
        }

        Activity stopped = Single();
        Assert.AreEqual(ActivityStatusCode.Error, stopped.Status);

        ActivityEvent exceptionEvent = stopped.Events.Single();
        Assert.AreEqual("exception", exceptionEvent.Name);
        Assert.AreEqual(typeof(InvalidOperationException).FullName, GetTag(exceptionEvent, "exception.type"));
        Assert.AreEqual("boom", GetTag(exceptionEvent, "exception.message"));
        Assert.IsNotNull(GetTag(exceptionEvent, "exception.stacktrace"));
    }

    [TestMethod]
    public void AddEvent_AddsANamedEventWithItsTags()
    {
        using (IPlatformActivity? activity = _service.StartActivity(Name("events")))
        {
            Assert.IsNotNull(activity);
            activity.AddEvent("hang.detected", [new("dump.path", "a.dmp")]);
        }

        ActivityEvent activityEvent = Single().Events.Single();
        Assert.AreEqual("hang.detected", activityEvent.Name);
        Assert.AreEqual("a.dmp", GetTag(activityEvent, "dump.path"));
    }

    [TestMethod]
    public void StartActivity_SetsTheTagsProvidedAtCreation()
    {
        using (_service.StartActivity(Name("tags"), tags: [new("test.case.name", "MyTest")]))
        {
        }

        Assert.AreEqual("MyTest", Single().GetTagItem("test.case.name"));
    }

    private static object? GetTag(ActivityEvent activityEvent, string key)
        => activityEvent.Tags
            .Where(tag => tag.Key == key)
            .Select(tag => tag.Value)
            .FirstOrDefault();

    private string Name(string name) => _namePrefix + name;

    private Activity Single()
    {
        lock (_stoppedActivities)
        {
            return _stoppedActivities.Single();
        }
    }
}
