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
