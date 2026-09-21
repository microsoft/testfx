// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

using Microsoft.Testing.Extensions.OpenTelemetry;
using Microsoft.Testing.Platform.Telemetry;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class ActivityWrapperTests : IDisposable
{
    private readonly string _sourceName = $"test-{Guid.NewGuid():N}";
    private readonly Activity? _ambientAtStart = Activity.Current;
    private readonly ActivitySource _source;
    private readonly ActivityListener _listener;
    private ActivitySamplingResult _samplingResult = ActivitySamplingResult.AllDataAndRecorded;

    public ActivityWrapperTests()
    {
        _source = new ActivitySource(_sourceName);
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == _sourceName,
            Sample = (ref _) => _samplingResult,
            SampleUsingParentId = (ref _) => _samplingResult,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose()
    {
        Activity.Current = _ambientAtStart;
        _listener.Dispose();
        _source.Dispose();
    }

    [TestMethod]
    public void TraceIdAndSpanId_WithW3CActivity_ReturnHexadecimalIdentifiers()
    {
        Activity activity = StartActivity("w3c");
        using ActivityWrapper wrapper = new(activity);

        Assert.AreEqual(ActivityIdFormat.W3C, activity.IdFormat);
        Assert.AreEqual(activity.TraceId.ToHexString(), wrapper.TraceId);
        Assert.AreEqual(activity.SpanId.ToHexString(), wrapper.SpanId);
    }

    [TestMethod]
    public void TraceIdAndSpanId_WithHierarchicalActivity_ReturnNull()
    {
        using Activity activity = new Activity("hierarchical")
            .SetIdFormat(ActivityIdFormat.Hierarchical)
            .Start();
        using ActivityWrapper wrapper = new(activity);

        Assert.AreEqual(ActivityIdFormat.Hierarchical, activity.IdFormat);
        Assert.IsNull(wrapper.TraceId);
        Assert.IsNull(wrapper.SpanId);
    }

    [TestMethod]
    public void IsRecording_ReflectsActivityIsAllDataRequested()
    {
        AssertRecordingState(ActivitySamplingResult.AllDataAndRecorded, expected: true);
        AssertRecordingState(ActivitySamplingResult.PropagationData, expected: false);
    }

    [TestMethod]
    public void SetTag_SetsUnderlyingTagAndReturnsSameWrapper()
    {
        Activity activity = StartActivity("set-tag");
        using ActivityWrapper wrapper = new(activity);

        IPlatformActivity returned = wrapper.SetTag("test.tag", "value");

        Assert.AreSame(wrapper, returned);
        Assert.AreEqual("value", activity.GetTagItem("test.tag"));
    }

    [TestMethod]
    public void RecordException_WithAdditionalTags_RecordsMergedExceptionEventAndErrorStatus()
    {
        Activity activity = StartActivity("record-exception");
        using ActivityWrapper wrapper = new(activity);
        InvalidOperationException exception = new("boom");

        wrapper.RecordException(exception, [new("test.additional", "value")]);

        Assert.AreEqual(ActivityStatusCode.Error, activity.Status);
        ActivityEvent exceptionEvent = activity.Events.Single();
        Assert.AreEqual("exception", exceptionEvent.Name);
        Assert.AreEqual(typeof(InvalidOperationException).FullName, GetTag(exceptionEvent, "exception.type"));
        Assert.AreEqual(exception.Message, GetTag(exceptionEvent, "exception.message"));
        Assert.AreEqual(exception.ToString(), GetTag(exceptionEvent, "exception.stacktrace"));
        Assert.AreEqual("value", GetTag(exceptionEvent, "test.additional"));
    }

    [TestMethod]
    public void Dispose_NonAmbientWrapper_PreservesCurrentActivity()
    {
        Activity nonAmbientActivity = StartActivity("non-ambient");
        Activity.Current = _ambientAtStart;
        using Activity ambientActivity = StartActivity("ambient");
        ActivityWrapper wrapper = new(nonAmbientActivity, isAmbient: false);

        wrapper.Dispose();

        Assert.AreSame(ambientActivity, Activity.Current);
    }

    private Activity StartActivity(string name)
        => _source.StartActivity(name, ActivityKind.Internal, default(ActivityContext))
            ?? throw new InvalidOperationException("The test activity listener did not create an activity.");

    private void AssertRecordingState(ActivitySamplingResult samplingResult, bool expected)
    {
        _samplingResult = samplingResult;
        Activity activity = StartActivity($"recording-{samplingResult}");
        using ActivityWrapper wrapper = new(activity);

        Assert.AreEqual(activity.IsAllDataRequested, wrapper.IsRecording);
        Assert.AreEqual(expected, wrapper.IsRecording);
    }

    private static object? GetTag(ActivityEvent activityEvent, string key)
        => activityEvent.Tags
            .Where(tag => tag.Key == key)
            .Select(tag => tag.Value)
            .FirstOrDefault();
}
