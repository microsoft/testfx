// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Reflection;

using Microsoft.Testing.Extensions.MSBuild;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

using Moq;

namespace Microsoft.Testing.Platform.MSBuild.UnitTests;

[TestClass]
public sealed class MSBuildConsumerTests
{
    private const string MSBuildNodeOptionKey = "internal-msbuild-node";

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task IsEnabledAsync_UsesMSBuildNodeOption(bool optionSet)
    {
        Mock<ICommandLineOptions> options = new(MockBehavior.Strict);
        options.Setup(x => x.IsOptionSet(MSBuildNodeOptionKey)).Returns(optionSet);
        MSBuildConsumer consumer = new(Mock.Of<IServiceProvider>(), options.Object);

        Assert.AreEqual(optionSet, await consumer.IsEnabledAsync());
        Assert.AreSequenceEqual(new[] { typeof(TestNodeUpdateMessage) }, consumer.DataTypesConsumed);
        options.VerifyAll();
    }

    [TestMethod]
    public async Task ConsumeAsync_PassedAndSkippedNodes_AccumulateOnlyTheirTerminalCounters()
    {
        MSBuildConsumer consumer = CreateConsumer();

        await ConsumeAsync(consumer, DiscoveredTestNodeStateProperty.CachedInstance);
        await ConsumeAsync(consumer, InProgressTestNodeStateProperty.CachedInstance);
        AssertCounters(consumer, total: 0, failed: 0, passed: 0, skipped: 0);

        await ConsumeAsync(consumer, PassedTestNodeStateProperty.CachedInstance);
        AssertCounters(consumer, total: 1, failed: 0, passed: 1, skipped: 0);

        await ConsumeAsync(consumer, SkippedTestNodeStateProperty.CachedInstance);
        AssertCounters(consumer, total: 2, failed: 0, passed: 1, skipped: 1);

        await ConsumeAsync(consumer, PassedTestNodeStateProperty.CachedInstance);
        AssertCounters(consumer, total: 3, failed: 0, passed: 2, skipped: 1);
    }

    [TestMethod]
    public async Task ConsumeAsync_UnknownDataOrMissingState_DoesNotChangeCounters()
    {
        MSBuildConsumer consumer = CreateConsumer();
        await ConsumeAsync(consumer, PassedTestNodeStateProperty.CachedInstance);

        await consumer.ConsumeAsync(null!, Mock.Of<IData>(), CancellationToken.None);
        await ConsumeAsync(consumer);
        await ConsumeAsync(consumer, new AssertionFailureProperty("expected", "actual"));

        AssertCounters(consumer, total: 1, failed: 0, passed: 1, skipped: 0);
    }

    [TestMethod]
    public async Task ConsumeAsync_UsesNodePropertiesRatherThanEnvelopeProperties()
    {
        MSBuildConsumer consumer = CreateConsumer();
        TestNodeUpdateMessage update = CreateUpdate();
        update.Properties.Add(PassedTestNodeStateProperty.CachedInstance);

        await consumer.ConsumeAsync(null!, update, CancellationToken.None);

        AssertCounters(consumer, total: 0, failed: 0, passed: 0, skipped: 0);

        update.TestNode.Properties.Add(SkippedTestNodeStateProperty.CachedInstance);
        await consumer.ConsumeAsync(null!, update, CancellationToken.None);

        AssertCounters(consumer, total: 1, failed: 0, passed: 0, skipped: 1);
    }

    [TestMethod]
    public async Task ConsumeAsync_SingleAncillaryProperties_AreAcceptedWithoutChangingPassedOutcome()
    {
        MSBuildConsumer consumer = CreateConsumer();

        await ConsumeAsync(
            consumer,
            PassedTestNodeStateProperty.CachedInstance,
            CreateDuplicateProperties("timing")[0],
            CreateDuplicateProperties("location")[0],
            CreateDuplicateProperties("assertion")[0],
            Mock.Of<IProperty>());

        AssertCounters(consumer, total: 1, failed: 0, passed: 1, skipped: 0);
    }

    [TestMethod]
    [DataRow("passed")]
    [DataRow("skipped")]
    [DataRow("failed")]
    [DataRow("error")]
    [DataRow("timeout")]
    [DataRow("canceled")]
    public async Task ConsumeAsync_SupersededRetryAttempt_IsIgnoredBeforeCountingOrReporting(string state)
    {
        MSBuildConsumer consumer = CreateConsumer();

        await ConsumeAsync(consumer, CreateState(state), new RetryAttemptProperty(1, isSuperseded: true));
        AssertCounters(consumer, total: 0, failed: 0, passed: 0, skipped: 0);

        await ConsumeAsync(consumer, PassedTestNodeStateProperty.CachedInstance, new RetryAttemptProperty(2, isSuperseded: false));
        AssertCounters(consumer, total: 1, failed: 0, passed: 1, skipped: 0);
    }

    [TestMethod]
    [DataRow("timing", false)]
    [DataRow("timing", true)]
    [DataRow("location", false)]
    [DataRow("location", true)]
    [DataRow("assertion", false)]
    [DataRow("assertion", true)]
    public async Task ConsumeAsync_DuplicateProperties_ThrowBeforeCountingEvenForSupersededRetries(string propertyKind, bool superseded)
    {
        MSBuildConsumer consumer = CreateConsumer();
        IProperty[] duplicates = CreateDuplicateProperties(propertyKind);
        TestNodeUpdateMessage update = CreateUpdate(
            PassedTestNodeStateProperty.CachedInstance,
            duplicates[0],
            duplicates[1],
            new RetryAttemptProperty(1, superseded));

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => consumer.ConsumeAsync(null!, update, CancellationToken.None));

        Assert.AreEqual($"Found multiple properties of type '{duplicates[0].GetType()}'.", exception.Message);
        AssertCounters(consumer, total: 0, failed: 0, passed: 0, skipped: 0);
    }

    [TestMethod]
    public async Task ConsumeAsync_CanceledToken_TakesPrecedenceOverEndedSessionAndInvalidMessage()
    {
        MSBuildConsumer consumer = CreateConsumer();
        SetPrivateField(consumer, "_sessionEnded", true);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        OperationCanceledException exception = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => consumer.ConsumeAsync(null!, null!, cancellation.Token));

        Assert.AreEqual(cancellation.Token, exception.CancellationToken);
        AssertCounters(consumer, total: 0, failed: 0, passed: 0, skipped: 0);
    }

    [TestMethod]
    public async Task ConsumeAsync_EndedSession_IgnoresEvenMalformedUpdates()
    {
        MSBuildConsumer consumer = CreateConsumer();
        await ConsumeAsync(consumer, PassedTestNodeStateProperty.CachedInstance);
        SetPrivateField(consumer, "_sessionEnded", true);
        IProperty[] duplicates = CreateDuplicateProperties("timing");

        await consumer.ConsumeAsync(
            null!,
            CreateUpdate(new FailedTestNodeStateProperty("late failure"), duplicates[0], duplicates[1]),
            CancellationToken.None);
        await ConsumeAsync(consumer, SkippedTestNodeStateProperty.CachedInstance);

        AssertCounters(consumer, total: 1, failed: 0, passed: 1, skipped: 0);
    }

    [TestMethod]
    [DataRow("failed", false)]
    [DataRow("failed", true)]
    [DataRow("error", false)]
    [DataRow("error", true)]
    [DataRow("timeout", false)]
    [DataRow("timeout", true)]
    [DataRow("canceled", false)]
    [DataRow("canceled", true)]
    public async Task ConsumeAsync_FailureWithoutInitializedPipe_CountsFailureThenRejectsInvalidLifecycle(string state, bool sessionStarted)
    {
        Mock<IServiceProvider> services = new();
        using MSBuildTestApplicationLifecycleCallbacks lifecycle = new(Mock.Of<IConfiguration>(), Mock.Of<ICommandLineOptions>());
        services.Setup(x => x.GetService(typeof(MSBuildTestApplicationLifecycleCallbacks))).Returns(lifecycle);
        MSBuildConsumer consumer = new(services.Object, Mock.Of<ICommandLineOptions>());
        if (sessionStarted)
        {
            await consumer.OnTestSessionStartingAsync(Mock.Of<ITestSessionContext>());
        }

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => ConsumeAsync(consumer, CreateState(state)));

        AssertCounters(consumer, total: 1, failed: 1, passed: 0, skipped: 0);
    }

    [TestMethod]
    public async Task OnTestSessionStartingAsync_ResolvesLifecycleAndRestartsStopwatch()
    {
        Mock<IServiceProvider> services = new(MockBehavior.Strict);
        using MSBuildTestApplicationLifecycleCallbacks lifecycle = new(Mock.Of<IConfiguration>(), Mock.Of<ICommandLineOptions>());
        services.Setup(x => x.GetService(typeof(MSBuildTestApplicationLifecycleCallbacks))).Returns(lifecycle);
        MSBuildConsumer consumer = new(services.Object, Mock.Of<ICommandLineOptions>());
        Stopwatch stopwatch = GetPrivateField<Stopwatch>(consumer, "_sessionStopwatch");

        Assert.IsFalse(stopwatch.IsRunning);

        await consumer.OnTestSessionStartingAsync(Mock.Of<ITestSessionContext>());

        Assert.AreSame(lifecycle, GetPrivateField<MSBuildTestApplicationLifecycleCallbacks>(consumer, "_msBuildTestApplicationLifecycleCallbacks"));
        Assert.IsTrue(stopwatch.IsRunning);
        AssertCounters(consumer, total: 0, failed: 0, passed: 0, skipped: 0);
        services.VerifyAll();
    }

    [TestMethod]
    public async Task OnTestSessionStartingAsync_MissingLifecycle_RejectsBeforeStartingStopwatch()
    {
        MSBuildConsumer consumer = CreateConsumer();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => consumer.OnTestSessionStartingAsync(Mock.Of<ITestSessionContext>()));

        Assert.IsFalse(GetPrivateField<Stopwatch>(consumer, "_sessionStopwatch").IsRunning);
        Assert.IsFalse(GetPrivateField<bool>(consumer, "_sessionEnded"));
    }

    [TestMethod]
    public async Task OnTestSessionFinishingAsync_MissingPipe_ClosesSessionBeforeSummaryFailure()
    {
        Mock<IServiceProvider> services = new();
        using MSBuildTestApplicationLifecycleCallbacks lifecycle = new(Mock.Of<IConfiguration>(), Mock.Of<ICommandLineOptions>());
        services.Setup(x => x.GetService(typeof(MSBuildTestApplicationLifecycleCallbacks))).Returns(lifecycle);
        MSBuildConsumer consumer = new(services.Object, Mock.Of<ICommandLineOptions>());
        await consumer.OnTestSessionStartingAsync(Mock.Of<ITestSessionContext>());
        await ConsumeAsync(consumer, PassedTestNodeStateProperty.CachedInstance);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => consumer.OnTestSessionFinishingAsync(Mock.Of<ITestSessionContext>()));

        Assert.IsTrue(GetPrivateField<bool>(consumer, "_sessionEnded"));
        Assert.IsFalse(GetPrivateField<Stopwatch>(consumer, "_sessionStopwatch").IsRunning);
        await ConsumeAsync(consumer, PassedTestNodeStateProperty.CachedInstance);
        AssertCounters(consumer, total: 1, failed: 0, passed: 1, skipped: 0);
    }

    [TestMethod]
    [DataRow(null, null)]
    [DataRow(-1d, null)]
    [DataRow(0d, "0ms")]
    [DataRow(9d, "9ms")]
    [DataRow(999d, "999ms")]
    [DataRow(1000d, "1s 000ms")]
    [DataRow(61002d, "1m 01s 002ms")]
    [DataRow(3600000d, "1h 00m 00s 000ms")]
    [DataRow(3661002d, "1h 01m 01s 002ms")]
    [DataRow(86400000d, "1d 00h 00m 00s 000ms")]
    [DataRow(90061002d, "1d 01h 01m 01s 002ms")]
    public void ToHumanReadableDuration_FormatsBoundariesWithPaddedChildUnits(double? duration, string? expected)
    {
        MethodInfo method = typeof(MSBuildConsumer).GetMethod("ToHumanReadableDuration", BindingFlags.Static | BindingFlags.NonPublic)!;

        Assert.AreEqual(expected, (string?)method.Invoke(null, [duration]));
    }

    [TestMethod]
    [DataRow("failed", false, DisplayName = "Failure explanation")]
    [DataRow("failed", true, DisplayName = "Failure exception")]
    [DataRow("error", false, DisplayName = "Error explanation")]
    [DataRow("error", true, DisplayName = "Error exception")]
    [DataRow("timeout", false, DisplayName = "Timeout explanation")]
    [DataRow("timeout", true, DisplayName = "Timeout exception")]
    [DataRow("canceled", false, DisplayName = "Legacy cancellation explanation")]
    [DataRow("canceled", true, DisplayName = "Legacy cancellation exception")]
    public async Task ConsumeAsync_TerminalFailure_SendsConcreteFailurePayload(string state, bool includeException)
    {
        await using MSBuildPipeFixture peer = new();
        using CancellationTokenSource operation = new(MSBuildPipeFixture.OperationLimit);
        using MSBuildTestApplicationLifecycleCallbacks lifecycle = CreateConnectedLifecycle(peer.PipeName);
        await lifecycle.BeforeRunAsync(operation.Token);
        MSBuildConsumer consumer = CreateConsumer(lifecycle, Mock.Of<ICommandLineOptions>());
        await consumer.OnTestSessionStartingAsync(Mock.Of<ITestSessionContext>());
        Exception exception = new InvalidOperationException("exception-message");
        TestNodeStateProperty failure = state switch
        {
            "failed" => includeException ? new FailedTestNodeStateProperty(exception, "explanation") : new FailedTestNodeStateProperty("explanation"),
            "error" => includeException ? new ErrorTestNodeStateProperty(exception, "explanation") : new ErrorTestNodeStateProperty("explanation"),
            "timeout" => includeException ? new TimeoutTestNodeStateProperty(exception, "explanation") : new TimeoutTestNodeStateProperty("explanation"),
#pragma warning disable CS0618, MTP0001 // Preserve wire compatibility with legacy cancellation producers.
            "canceled" => includeException ? new CancelledTestNodeStateProperty(exception, "explanation") : new CancelledTestNodeStateProperty("explanation"),
#pragma warning restore CS0618, MTP0001
            _ => throw new ArgumentOutOfRangeException(nameof(state)),
        };
        TestNodeUpdateMessage update = CreateUpdate(
            failure,
            new TimingProperty(new TimingInfo(DateTimeOffset.MinValue, DateTimeOffset.MinValue, TimeSpan.FromMilliseconds(61002))),
            new TestFileLocationProperty("tests with spaces.cs", new LinePositionSpan(new LinePosition(42, 3), new LinePosition(43, 7))));

        await consumer.ConsumeAsync(null!, update, operation.Token);

        MSBuildFailureInfo request = Assert.ContainsSingle(peer.Received<MSBuildFailureInfo>());
        bool expectedCanceled = state is "timeout" or "canceled";
        string actualAssertionExpectation = request.Expected;
        Assert.AreEqual("Test", request.DisplayName);
        Assert.AreEqual(expectedCanceled, request.IsCanceled);
        Assert.AreEqual("1m 01s 002ms", request.Duration);
        Assert.AreEqual(includeException ? "exception-message" : "explanation", request.ErrorMessage);
        Assert.AreEqual(string.Empty, request.ErrorStackTrace);
        Assert.AreEqual(string.Empty, actualAssertionExpectation);
        Assert.AreEqual(string.Empty, request.Actual);
        Assert.AreEqual("tests with spaces.cs", request.CodeFilePath);
        Assert.AreEqual(42, request.LineNumber);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "Exception assertion data fallback")]
    [DataRow(true, DisplayName = "Assertion property overrides exception data")]
    public async Task ConsumeAsync_FailedAssertion_PreservesStackAndExpectedActualPrecedence(bool assertionProperty)
    {
        await using MSBuildPipeFixture peer = new();
        using CancellationTokenSource operation = new(MSBuildPipeFixture.OperationLimit);
        using MSBuildTestApplicationLifecycleCallbacks lifecycle = CreateConnectedLifecycle(peer.PipeName);
        await lifecycle.BeforeRunAsync(operation.Token);
        MSBuildConsumer consumer = CreateConsumer(lifecycle, Mock.Of<ICommandLineOptions>());
        await consumer.OnTestSessionStartingAsync(Mock.Of<ITestSessionContext>());
        Exception failure = CaptureFailure();
        failure.Data["assert.expected"] = "fallback-expected";
        failure.Data["assert.actual"] = "fallback-actual";
        List<IProperty> properties = [new FailedTestNodeStateProperty(failure, "ignored-explanation")];
        if (assertionProperty)
        {
            properties.Add(new AssertionFailureProperty("property-expected", "property-actual"));
        }

        await consumer.ConsumeAsync(null!, CreateUpdate([.. properties]), operation.Token);

        MSBuildFailureInfo request = Assert.ContainsSingle(peer.Received<MSBuildFailureInfo>());
        string? expectedStackTrace = failure.StackTrace;
        string expectedAssertionExpectation = assertionProperty ? "property-expected" : "fallback-expected";
        string actualAssertionExpectation = request.Expected;
        Assert.AreEqual("captured failure", request.ErrorMessage);
        Assert.AreEqual(expectedStackTrace, request.ErrorStackTrace);
        Assert.Contains(nameof(CaptureFailure), request.ErrorStackTrace);
        Assert.AreEqual(expectedAssertionExpectation, actualAssertionExpectation);
        Assert.AreEqual(assertionProperty ? "property-actual" : "fallback-actual", request.Actual);
        Assert.AreEqual(string.Empty, request.Duration);
        Assert.AreEqual(string.Empty, request.CodeFilePath);
        Assert.AreEqual(0, request.LineNumber);
    }

    [TestMethod]
    [DataRow(null, true, DisplayName = "Default skipped policy")]
    [DataRow("allow-skipped", true, DisplayName = "Explicit allow-skipped policy")]
    [DataRow("STRICT", false, DisplayName = "Case-insensitive strict policy")]
    public async Task OnTestSessionFinishingAsync_SendsSummaryOfFinalOutcomesAndSkippedPolicy(string? zeroTestsPolicy, bool allowSkipped)
    {
        await using MSBuildPipeFixture peer = new();
        using CancellationTokenSource operation = new(MSBuildPipeFixture.OperationLimit);
        using MSBuildTestApplicationLifecycleCallbacks lifecycle = CreateConnectedLifecycle(peer.PipeName);
        await lifecycle.BeforeRunAsync(operation.Token);
        Mock<ICommandLineOptions> options = new(MockBehavior.Strict);
        string[]? arguments = zeroTestsPolicy is null ? null : [zeroTestsPolicy];
        options.Setup(x => x.TryGetOptionArgumentList("zero-tests-policy", out arguments)).Returns(zeroTestsPolicy is not null);
        MSBuildConsumer consumer = CreateConsumer(lifecycle, options.Object);
        await consumer.OnTestSessionStartingAsync(Mock.Of<ITestSessionContext>());
        await ConsumeAsync(consumer, DiscoveredTestNodeStateProperty.CachedInstance);
        await ConsumeAsync(consumer, InProgressTestNodeStateProperty.CachedInstance);
        await ConsumeAsync(consumer, new FailedTestNodeStateProperty("superseded"), new RetryAttemptProperty(1, isSuperseded: true));
        await ConsumeAsync(consumer, PassedTestNodeStateProperty.CachedInstance, new RetryAttemptProperty(2, isSuperseded: false));
        await ConsumeAsync(consumer, PassedTestNodeStateProperty.CachedInstance);
        await ConsumeAsync(consumer, SkippedTestNodeStateProperty.CachedInstance);
        await ConsumeAsync(consumer, new ErrorTestNodeStateProperty("final-error"));
        Mock<ITestSessionContext> session = new();
        session.SetupGet(x => x.CancellationToken).Returns(operation.Token);

        await consumer.OnTestSessionFinishingAsync(session.Object);
        await ConsumeAsync(consumer, new FailedTestNodeStateProperty("late-failure"));
        await ConsumeAsync(consumer, PassedTestNodeStateProperty.CachedInstance);

        MSBuildSummaryInfo summary = Assert.ContainsSingle(peer.Received<MSBuildSummaryInfo>());
        Assert.AreEqual(4, summary.Total);
        Assert.AreEqual(1, summary.Failed);
        Assert.AreEqual(2, summary.Passed);
        Assert.AreEqual(1, summary.Skipped);
        Assert.AreEqual(allowSkipped, summary.AllowSkipped);
        Assert.Contains("ms", summary.Duration);
        MSBuildFailureInfo failure = Assert.ContainsSingle(peer.Received<MSBuildFailureInfo>());
        Assert.AreEqual("final-error", failure.ErrorMessage);
        options.VerifyAll();
        options.VerifyNoOtherCalls();
    }

    private static MSBuildTestApplicationLifecycleCallbacks CreateConnectedLifecycle(string pipeName)
    {
        Mock<IConfiguration> configuration = new();
        configuration.Setup(x => x["platformOptions:resultDirectory"]).Returns("consumer-results");
        Mock<ICommandLineOptions> options = new();
        string[]? arguments = [pipeName];
        options.Setup(x => x.TryGetOptionArgumentList(MSBuildNodeOptionKey, out arguments)).Returns(true);
        return new(configuration.Object, options.Object);
    }

    private static MSBuildConsumer CreateConsumer(MSBuildTestApplicationLifecycleCallbacks lifecycle, ICommandLineOptions options)
    {
        Mock<IServiceProvider> services = new();
        services.Setup(x => x.GetService(typeof(MSBuildTestApplicationLifecycleCallbacks))).Returns(lifecycle);
        return new(services.Object, options);
    }

    private static Exception CaptureFailure()
    {
        try
        {
            throw new InvalidOperationException("captured failure");
        }
        catch (InvalidOperationException exception)
        {
            return exception;
        }
    }

    private static MSBuildConsumer CreateConsumer()
        => new(Mock.Of<IServiceProvider>(), Mock.Of<ICommandLineOptions>());

    private static Task ConsumeAsync(MSBuildConsumer consumer, params IProperty[] properties)
        => consumer.ConsumeAsync(null!, CreateUpdate(properties), CancellationToken.None);

    private static TestNodeUpdateMessage CreateUpdate(params IProperty[] properties)
        => new(new SessionUid("session"), new TestNode
        {
            Uid = "test",
            DisplayName = "Test",
            Properties = new PropertyBag(properties),
        });

    private static TestNodeStateProperty CreateState(string state)
        => state switch
        {
            "passed" => PassedTestNodeStateProperty.CachedInstance,
            "skipped" => SkippedTestNodeStateProperty.CachedInstance,
            "failed" => new FailedTestNodeStateProperty("failure"),
            "error" => new ErrorTestNodeStateProperty("error"),
            "timeout" => new TimeoutTestNodeStateProperty("timeout"),
#pragma warning disable CS0618, MTP0001 // Exercise compatibility with legacy producers of the obsolete cancellation state.
            "canceled" => new CancelledTestNodeStateProperty("canceled"),
#pragma warning restore CS0618, MTP0001
            _ => throw new ArgumentOutOfRangeException(nameof(state)),
        };

    private static IProperty[] CreateDuplicateProperties(string kind)
        => kind switch
        {
            "timing" =>
            [
                new TimingProperty(new TimingInfo(DateTimeOffset.MinValue, DateTimeOffset.MinValue, TimeSpan.Zero)),
                new TimingProperty(new TimingInfo(DateTimeOffset.MinValue, DateTimeOffset.MinValue, TimeSpan.FromSeconds(1))),
            ],
            "location" =>
            [
                new TestFileLocationProperty("first.cs", new LinePositionSpan(new LinePosition(1, 0), new LinePosition(1, 1))),
                new TestFileLocationProperty("second.cs", new LinePositionSpan(new LinePosition(2, 0), new LinePosition(2, 1))),
            ],
            "assertion" => [new AssertionFailureProperty("first", "actual"), new AssertionFailureProperty("second", "actual")],
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

    // The counters have no public observation seam without sending a summary over real IPC.
    private static void AssertCounters(MSBuildConsumer consumer, int total, int failed, int passed, int skipped)
    {
        Assert.AreEqual(total, GetPrivateField<int>(consumer, "_totalTests"));
        Assert.AreEqual(failed, GetPrivateField<int>(consumer, "_totalFailedTests"));
        Assert.AreEqual(passed, GetPrivateField<int>(consumer, "_totalPassedTests"));
        Assert.AreEqual(skipped, GetPrivateField<int>(consumer, "_totalSkippedTests"));
    }

    private static T GetPrivateField<T>(MSBuildConsumer consumer, string name)
        => (T)typeof(MSBuildConsumer).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(consumer)!;

    private static void SetPrivateField<T>(MSBuildConsumer consumer, string name, T value)
        => typeof(MSBuildConsumer).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(consumer, value);
}
