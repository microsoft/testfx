// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.UnitTests;

internal sealed class TestNodeTimingData
{
    private TestNodeTimingData(
        TimingProperty? timing,
        double? expectedDuration,
        string? expectedStartUtc,
        string? expectedStopUtc,
        IProperty? state,
        string? executionState,
        string? explanation = null,
        string cultureName = "en-US")
    {
        PropertyBag properties = new();
        if (state is not null)
        {
            properties.Add(state);
        }

        if (timing is not null)
        {
            properties.Add(timing);
        }

        TestNode = new TestNode
        {
            Uid = "timed-test",
            DisplayName = "TimedTest",
            Properties = properties,
        };
        ExpectedDuration = expectedDuration;
        ExpectedStartUtc = expectedStartUtc;
        ExpectedStopUtc = expectedStopUtc;
        CultureName = cultureName;

        string durationField = expectedDuration is null ? string.Empty : $$"""
            ,"time.duration-ms":{{expectedDuration.Value.ToString("R", CultureInfo.InvariantCulture)}}
            """;
        string startField = expectedStartUtc is null ? string.Empty : $$"""
            ,"time.start-utc":"{{expectedStartUtc}}"
            """;
        string stopField = expectedStopUtc is null ? string.Empty : $$"""
            ,"time.stop-utc":"{{expectedStopUtc}}"
            """;
        string stateFields = executionState is null ? """
            ,"node-type":"group"
            """ : $$"""
            ,"node-type":"action","execution-state":"{{executionState}}"
            """;
        string errorField = explanation is null ? string.Empty : $$"""
            ,"error.message":"{{explanation}}"
            """;
        ExpectedJson = EscapeUtcOffsets($$"""
            {"uid":"timed-test","display-name":"TimedTest"{{durationField}}{{startField}}{{stopField}}{{stateFields}}{{errorField}}}
            """);
#if !NETCOREAPP
        // Jsonite keeps a space after each key separator in this flat object. Preserve the exact
        // expectation instead of removing spaces from values such as the failure explanation.
        ExpectedJson = ExpectedJson.Replace("\":", "\": ");
#endif
    }

    public TestNode TestNode { get; }

    public double? ExpectedDuration { get; }

    public string? ExpectedStartUtc { get; }

    public string? ExpectedStopUtc { get; }

    public string ExpectedJson { get; }

    public string CultureName { get; }

    // Only these controlled IDs enter test names. Timestamps, cultures, and JSON stay in the data.
    public static IEnumerable<object[]> Cases
        => new[]
        {
            "OffsetsFrench",
            "OffsetsArabic",
            "AbsentGroup",
            "AbsentPassed",
            "DefaultTiming",
            "DurationOnly",
            "NegativeDurationOnly",
            "MinimumInstantWithOffsets",
            "StartOnly",
            "StopOnly",
            "ZeroDurationWithKnownTimes",
            "NegativeDurationWithKnownTimes",
            "ReversedInterval",
            "FirstTickAfterMinimum",
            "MaximumTimestamp",
            "StateDiscovered",
            "StateInProgress",
            "StatePassed",
            "StateSkipped",
            "StateFailed",
            "StateError",
            "StateTimedOut",
            "StateCanceled",
            "StateExecutionCompleted",
        }.Select(caseName => new object[] { caseName });

    public static string GetDisplayName(MethodInfo methodInfo, object?[]? data)
        => $"{methodInfo.DeclaringType!.Name}.{methodInfo.Name}_{data![0]}";

    public static TestNodeTimingData Create(string caseName)
    {
        // Both endpoints cross a UTC date boundary in opposite directions and retain all seven digits.
        DateTimeOffset start = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.FromMinutes(330)).AddTicks(1234567);
        DateTimeOffset stop = new DateTimeOffset(2026, 10, 31, 23, 59, 58, TimeSpan.FromHours(-4)).AddTicks(7654321);
        const string StartUtc = "2026-10-31T18:30:00.1234567+00:00";
        const string StopUtc = "2026-11-01T03:59:58.7654321+00:00";

        // Deliberately not stop-start. The producer must retain the independently supplied duration.
        TimingInfo knownTiming = new(start, stop, TimeSpan.FromTicks(372500));

#pragma warning disable CS0618, MTP0001 // Exercise the existing canceled wire state while it remains supported.
        return caseName switch
        {
            "OffsetsFrench" => WithTiming(knownTiming, 37.25, StartUtc, StopUtc, cultureName: "fr-FR"),
            "OffsetsArabic" => WithTiming(knownTiming, 37.25, StartUtc, StopUtc, cultureName: "ar-SA"),
            "AbsentGroup" => new(null, null, null, null, null, null),
            "AbsentPassed" => new(null, null, null, null, PassedTestNodeStateProperty.CachedInstance, "passed"),
            "DefaultTiming" => WithTiming(default, 0, null, null),
            "DurationOnly" => WithTiming(new TimingInfo(DateTimeOffset.MinValue, DateTimeOffset.MinValue, TimeSpan.FromTicks(255000)), 25.5, null, null),
            "NegativeDurationOnly" => WithTiming(new TimingInfo(default, default, TimeSpan.FromTicks(-75000)), -7.5, null, null),
            "MinimumInstantWithOffsets" => WithTiming(
                new TimingInfo(DateTimeOffset.MinValue.ToOffset(TimeSpan.FromHours(1)), DateTimeOffset.MinValue.ToOffset(TimeSpan.FromHours(2)), TimeSpan.FromTicks(255000)),
                25.5, null, null),
            "StartOnly" => WithTiming(
                new TimingInfo(start, default, TimeSpan.FromTicks(255000)), 25.5, StartUtc, null,
                InProgressTestNodeStateProperty.CachedInstance, "in-progress"),
            "StopOnly" => WithTiming(
                new TimingInfo(default, stop, TimeSpan.FromTicks(255000)), 25.5, null, StopUtc,
                new CancelledTestNodeStateProperty("timing cancellation"), "canceled", "timing cancellation"),
            "ZeroDurationWithKnownTimes" => WithTiming(new TimingInfo(start, stop, TimeSpan.Zero), 0, StartUtc, StopUtc),
            "NegativeDurationWithKnownTimes" => WithTiming(new TimingInfo(start, stop, TimeSpan.FromTicks(-75000)), -7.5, StartUtc, StopUtc),
            "ReversedInterval" => WithTiming(
                new TimingInfo(stop, start, TimeSpan.FromTicks(372500)), 37.25, StopUtc, StartUtc,
                new FailedTestNodeStateProperty("timing failure"), "failed", "timing failure"),
            "FirstTickAfterMinimum" => WithTiming(
                new TimingInfo(DateTimeOffset.MinValue.AddTicks(1), DateTimeOffset.MinValue.AddTicks(2), TimeSpan.Zero),
                0, "0001-01-01T00:00:00.0000001+00:00", "0001-01-01T00:00:00.0000002+00:00"),
            "MaximumTimestamp" => WithTiming(
                new TimingInfo(DateTimeOffset.MaxValue.AddTicks(-1), DateTimeOffset.MaxValue, TimeSpan.FromTicks(10000)),
                1, "9999-12-31T23:59:59.9999998+00:00", "9999-12-31T23:59:59.9999999+00:00"),
            "StateDiscovered" => WithTiming(knownTiming, 37.25, StartUtc, StopUtc, DiscoveredTestNodeStateProperty.CachedInstance, "discovered"),
            "StateInProgress" => WithTiming(knownTiming, 37.25, StartUtc, StopUtc, InProgressTestNodeStateProperty.CachedInstance, "in-progress"),
            "StatePassed" => WithTiming(knownTiming, 37.25, StartUtc, StopUtc),
            "StateSkipped" => WithTiming(knownTiming, 37.25, StartUtc, StopUtc, new SkippedTestNodeStateProperty("timing skip"), "skipped", "timing skip"),
            "StateFailed" => WithTiming(knownTiming, 37.25, StartUtc, StopUtc, new FailedTestNodeStateProperty("timing failure"), "failed", "timing failure"),
            "StateError" => WithTiming(knownTiming, 37.25, StartUtc, StopUtc, new ErrorTestNodeStateProperty("timing error"), "error", "timing error"),
            "StateTimedOut" => WithTiming(knownTiming, 37.25, StartUtc, StopUtc, new TimeoutTestNodeStateProperty("timing timeout"), "timed-out", "timing timeout"),
            "StateCanceled" => WithTiming(knownTiming, 37.25, StartUtc, StopUtc, new CancelledTestNodeStateProperty("timing cancellation"), "canceled", "timing cancellation"),
            "StateExecutionCompleted" => WithTiming(knownTiming, 37.25, StartUtc, StopUtc, TestNodeExecutionCompletedProperty.CachedInstance, "discovered"),
            _ => throw new ArgumentOutOfRangeException(nameof(caseName), caseName, "Unknown timing test case."),
        };
#pragma warning restore CS0618, MTP0001
    }

    public static string EscapeUtcOffsets(string expectedJson)
#if NETCOREAPP
        // Utf8JsonWriter escapes '+' in strings; the JSON value remains the offset-bearing UTC text.
        => expectedJson.Replace("+", "\\u002B");
#else
        => expectedJson;
#endif

    private static TestNodeTimingData WithTiming(
        TimingInfo timing,
        double expectedDuration,
        string? expectedStartUtc,
        string? expectedStopUtc,
        IProperty? state = null,
        string executionState = "passed",
        string? explanation = null,
        string cultureName = "en-US")
        => new(
            new TimingProperty(timing), expectedDuration, expectedStartUtc, expectedStopUtc,
            state ?? PassedTestNodeStateProperty.CachedInstance, executionState, explanation, cultureName);
}
