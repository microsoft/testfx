// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
[UnsupportedOSPlatform("browser")]
public sealed class DotnetTestDataConsumerTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(CoverageScopeLevel.Overall, null, DisplayName = "OverallCoverageIdentity")]
    [DataRow(CoverageScopeLevel.Module, "module with space.dll", DisplayName = "ModuleCoverageIdentity")]
    public async Task ConsumeAsync_CoverageSummaryPreservesIdentityAndInt64Counts(CoverageScopeLevel level, string? scopeName)
    {
        using DotnetTestConnectionTests.ConnectionFixture fixture = DotnetTestConnectionTests.CreateConnection(TestContext.CancellationToken);
        Assert.IsTrue(await DotnetTestConnectionTests.NegotiateCoverageSummaryAsync(fixture, TestContext.CancellationToken));
        List<CoverageSummaryMessages> sent = [];
        fixture.Client.RespondVoid<CoverageSummaryMessages>(TestContext.CancellationToken, sent.Add);
        var consumer = new DotnetTestDataConsumer(fixture.Connection, fixture.Environment.Object);
        var message = new TestCoverageMessage(
            new SessionUid("coverage-session"), new CoverageScope(level, scopeName), CoverageMetric.Custom,
            3_000_000_000L, long.MaxValue, "collector-id", "MC/DC");

        await consumer.ConsumeAsync(Mock.Of<IDataProducer>(), message, TestContext.CancellationToken);

        CoverageSummaryMessages batch = Assert.ContainsSingle(sent);
        Assert.AreEqual("execution", batch.ExecutionId);
        Assert.AreEqual(DotnetTestConnection.InstanceId, batch.InstanceId);
        Assert.AreEqual(
            new CoverageSummaryMessage("coverage-session", "collector-id", (int)level, scopeName, 255, "MC/DC", 3_000_000_000L, long.MaxValue),
            Assert.ContainsSingle(batch.Summaries));
    }

    [TestMethod]
    public async Task ConsumeAsync_CoverageUpdatesRemainReplacementsWithDistinctIdentities()
    {
        using DotnetTestConnectionTests.ConnectionFixture fixture = DotnetTestConnectionTests.CreateConnection(TestContext.CancellationToken);
        Assert.IsTrue(await DotnetTestConnectionTests.NegotiateCoverageSummaryAsync(fixture, TestContext.CancellationToken));
        List<CoverageSummaryMessages> sent = [];
        fixture.Client.RespondVoid<CoverageSummaryMessages>(TestContext.CancellationToken, sent.Add);
        var consumer = new DotnetTestDataConsumer(fixture.Connection, fixture.Environment.Object);
        TestCoverageMessage[] updates =
        [
            new(new SessionUid("s1"), CoverageScope.Overall, CoverageMetric.Line, 10, 20, "p1"),
            new(new SessionUid("s1"), CoverageScope.Overall, CoverageMetric.Line, 7, 12, "p1"),
            new(new SessionUid("s2"), CoverageScope.Overall, CoverageMetric.Line, 3, 9, "p1"),
            new(new SessionUid("s1"), CoverageScope.Overall, CoverageMetric.Line, 4, 11, "p2"),
            new(new SessionUid("s1"), CoverageScope.Overall, CoverageMetric.Branch, 2, 8, "p1"),
            new(new SessionUid("s1"), CoverageScope.Overall, CoverageMetric.Custom, 1, 6, "p1", "mcdc"),
            new(new SessionUid("s1"), CoverageScope.Overall, CoverageMetric.Custom, 0, 0, "p1", "paths"),
        ];

        foreach (TestCoverageMessage update in updates)
        {
            await consumer.ConsumeAsync(Mock.Of<IDataProducer>(), update, TestContext.CancellationToken);
        }

        Assert.HasCount(7, sent);
        Assert.AreSequenceEqual(
            [
                new CoverageSummaryMessage("s1", "p1", 0, null, 0, null, 10, 20),
                new CoverageSummaryMessage("s1", "p1", 0, null, 0, null, 7, 12),
                new CoverageSummaryMessage("s2", "p1", 0, null, 0, null, 3, 9),
                new CoverageSummaryMessage("s1", "p2", 0, null, 0, null, 4, 11),
                new CoverageSummaryMessage("s1", "p1", 0, null, 2, null, 2, 8),
                new CoverageSummaryMessage("s1", "p1", 0, null, 255, "mcdc", 1, 6),
                new CoverageSummaryMessage("s1", "p1", 0, null, 255, "paths", 0, 0),
            ],
            sent.Select(batch => Assert.ContainsSingle(batch.Summaries)));
        Assert.IsTrue(sent.All(batch => batch.ExecutionId == "execution" && batch.InstanceId == DotnetTestConnection.InstanceId));
    }

    [TestMethod]
    [DataRow(CoverageScopeLevel.Assembly, DisplayName = "AssemblyCoverageNotForwarded")]
    [DataRow(CoverageScopeLevel.Namespace, DisplayName = "NamespaceCoverageNotForwarded")]
    [DataRow(CoverageScopeLevel.Type, DisplayName = "TypeCoverageNotForwarded")]
    [DataRow(CoverageScopeLevel.File, DisplayName = "FileCoverageNotForwarded")]
    public async Task ConsumeAsync_NarrowCoverageScopesAreIgnored(CoverageScopeLevel level)
    {
        using DotnetTestConnectionTests.ConnectionFixture fixture = DotnetTestConnectionTests.CreateConnection(TestContext.CancellationToken);
        Assert.IsTrue(await DotnetTestConnectionTests.NegotiateCoverageSummaryAsync(fixture, TestContext.CancellationToken));
        fixture.Client.Mock.Invocations.Clear();
        var consumer = new DotnetTestDataConsumer(fixture.Connection, fixture.Environment.Object);

        await consumer.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            new TestCoverageMessage(new SessionUid("session"), new CoverageScope(level, "identity"), CoverageMetric.Line, 1, 2, "producer"),
            TestContext.CancellationToken);

        Assert.IsEmpty(fixture.Client.Mock.Invocations);
    }

    [TestMethod]
    [DataRow("1.0.0", null, DisplayName = "ConsumerOldPeer")]
    [DataRow("1.3.0", "false", DisplayName = "ConsumerOptOut")]
    [DataRow("1.3.0", "2", DisplayName = "ConsumerUnknownCoverage")]
    [DataRow("2.0.0", "1", DisplayName = "ConsumerIncompatibleBase")]
    public async Task ConsumeAsync_UnnegotiatedCoverageDoesNotSuppressArtifactsOrTestResults(string baseVersion, string? coverageVersion)
    {
        using DotnetTestConnectionTests.ConnectionFixture fixture = DotnetTestConnectionTests.CreateConnection(TestContext.CancellationToken);
        Assert.AreEqual(
            baseVersion != "2.0.0",
            await DotnetTestConnectionTests.NegotiateCoverageSummaryAsync(fixture, TestContext.CancellationToken, baseVersion, coverageVersion));
        List<object> sent = [];
        fixture.Client.RespondVoid<CoverageSummaryMessages>(TestContext.CancellationToken, value => sent.Add(value));
        fixture.Client.RespondVoid<FileArtifactMessages>(TestContext.CancellationToken, value => sent.Add(value));
        fixture.Client.RespondVoid<TestResultMessages>(TestContext.CancellationToken, value => sent.Add(value));
        var consumer = new DotnetTestDataConsumer(fixture.Connection, fixture.Environment.Object);
        var file = new FileInfo(Path.Combine(Path.GetTempPath(), $"coverage-consumer-{Guid.NewGuid():N}.fixture"));

        await consumer.ConsumeAsync(
            Mock.Of<IDataProducer>(), new TestCoverageMessage(new SessionUid("session"), CoverageScope.Overall, CoverageMetric.Line, 1, 2, "producer"),
            TestContext.CancellationToken);
        await consumer.ConsumeAsync(Mock.Of<IDataProducer>(), new FileArtifact(file, "original"), TestContext.CancellationToken);
        await consumer.ConsumeAsync(Mock.Of<IDataProducer>(), CreateUpdate(PassedTestNodeStateProperty.CachedInstance), TestContext.CancellationToken);

        Assert.HasCount(2, sent);
        FileArtifactMessage artifact = Assert.ContainsSingle(Assert.IsInstanceOfType<FileArtifactMessages>(sent[0]).FileArtifacts);
        Assert.AreEqual(file.FullName, artifact.FullPath);
        Assert.AreEqual("original", artifact.DisplayName);
        Assert.IsNull(artifact.CoverageSummary);
        SuccessfulTestResultMessage result = Assert.ContainsSingle(Assert.IsInstanceOfType<TestResultMessages>(sent[1]).SuccessfulTestMessages);
        Assert.AreEqual("uid", result.Uid);
        Assert.AreEqual(TestStates.Passed, result.State);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ConsumeAsync_DiscoveryNegotiationControlsFullMetadataAndOrderedTraits(bool ide)
    {
        using DotnetTestConnectionTests.ConnectionFixture fixture = DotnetTestConnectionTests.CreateConnection(TestContext.CancellationToken);
        fixture.Client.Respond(
            new HandshakeMessage(new()
            {
                [HandshakeMessagePropertyNames.SupportedProtocolVersions] = "1.3.0",
                [HandshakeMessagePropertyNames.IsIDE] = ide.ToString(),
            }), TestContext.CancellationToken);
        Assert.IsTrue(await fixture.Connection.IsCompatibleProtocolAsync(HandshakeMessageHostTypes.TestHost));
        List<DiscoveredTestMessages> sent = [];
        fixture.Client.RespondVoid<DiscoveredTestMessages>(TestContext.CancellationToken, sent.Add);
        var consumer = new DotnetTestDataConsumer(fixture.Connection, fixture.Environment.Object);
        TestNodeUpdateMessage message = CreateUpdate(
            DiscoveredTestNodeStateProperty.CachedInstance,
            new TestFileLocationProperty("source.cs", new LinePositionSpan(new(12, 3), new(14, 5))),
            new TestMethodIdentifierProperty("assembly", "Sample.Namespace", "SampleType", "SampleMethod", 0, ["System.Int32"], "System.Void"),
            new TestMetadataProperty("category", "first"),
            new TestMetadataProperty("category", "second"));

        await consumer.ConsumeAsync(Mock.Of<IDataProducer>(), message, TestContext.CancellationToken);

        DiscoveredTestMessages batch = Assert.ContainsSingle(sent);
        Assert.AreEqual("execution", batch.ExecutionId);
        Assert.AreEqual(DotnetTestConnection.InstanceId, batch.InstanceId);
        DiscoveredTestMessage discovered = Assert.ContainsSingle(batch.DiscoveredMessages);
        Assert.AreEqual("uid", discovered.Uid);
        Assert.AreEqual("DisplayName", discovered.DisplayName);
        Assert.AreEqual(ide ? "source.cs" : null, discovered.FilePath);
        Assert.AreEqual(ide ? 12 : null, discovered.LineNumber);
        Assert.AreEqual(ide ? "Sample.Namespace" : null, discovered.Namespace);
        Assert.AreEqual(ide ? "SampleType" : null, discovered.TypeName);
        Assert.AreEqual(ide ? "SampleMethod" : null, discovered.MethodName);
        if (ide)
        {
            Assert.AreSequenceEqual(["System.Int32"], discovered.ParameterTypeFullNames);
            // PropertyBag prepends constructor inputs; preserve its traversal order on the wire.
            Assert.AreSequenceEqual([new TraitMessage("category", "second"), new TraitMessage("category", "first")], discovered.Traits);
        }
        else
        {
            Assert.IsNull(discovered.ParameterTypeFullNames);
            Assert.IsEmpty(discovered.Traits);
        }
    }

    [TestMethod]
    [DataRow("passed", TestStates.Passed, true)]
    [DataRow("skipped", TestStates.Skipped, false)]
    [DataRow("progress", TestStates.InProgress, false)]
    public async Task ConsumeAsync_SuccessfulStatesPreserveOutputsRetryAndStateSpecificDuration(string outcome, byte expectedState, bool hasDuration)
    {
        using DotnetTestConnectionTests.ConnectionFixture fixture = DotnetTestConnectionTests.CreateConnection(TestContext.CancellationToken);
        fixture.Client.Respond(
            new HandshakeMessage(new()
            {
                [HandshakeMessagePropertyNames.SupportedProtocolVersions] = "1.3.0",
                [HandshakeMessagePropertyNames.IsIDE] = "true",
            }), TestContext.CancellationToken);
        Assert.IsTrue(await fixture.Connection.IsCompatibleProtocolAsync(HandshakeMessageHostTypes.TestHost));
        List<TestResultMessages> sent = [];
        fixture.Client.RespondVoid<TestResultMessages>(TestContext.CancellationToken, sent.Add);
        TestNodeStateProperty state = outcome switch
        {
            "passed" => new PassedTestNodeStateProperty("passed reason"),
            "skipped" => new SkippedTestNodeStateProperty("skipped reason"),
            _ => new InProgressTestNodeStateProperty("progress reason"),
        };
        var consumer = new DotnetTestDataConsumer(fixture.Connection, fixture.Environment.Object);
        TestNodeUpdateMessage message = CreateUpdate(
            state, FixedTiming(), new StandardOutputProperty("stdout"), new StandardErrorProperty("stderr"), new RetryAttemptProperty(3, true));

        await consumer.ConsumeAsync(Mock.Of<IDataProducer>(), message, TestContext.CancellationToken);

        TestResultMessages batch = Assert.ContainsSingle(sent);
        Assert.AreEqual("execution", batch.ExecutionId);
        Assert.IsEmpty(batch.FailedTestMessages);
        SuccessfulTestResultMessage result = Assert.ContainsSingle(batch.SuccessfulTestMessages);
        Assert.AreEqual("uid", result.Uid);
        Assert.AreEqual("DisplayName", result.DisplayName);
        Assert.AreEqual("session", result.SessionUid);
        Assert.AreEqual(expectedState, result.State);
        Assert.AreEqual(hasDuration ? 123L : null, result.Duration);
        Assert.AreEqual(outcome == "progress" ? string.Empty : outcome + " reason", result.Reason);
        Assert.AreEqual("stdout", result.StandardOutput);
        Assert.AreEqual("stderr", result.ErrorOutput);
        Assert.AreEqual(3, result.RetryAttemptNumber);
        Assert.IsTrue(result.IsSuperseded);
    }

    [TestMethod]
    [DataRow("failed", TestStates.Failed)]
    [DataRow("error", TestStates.Error)]
    [DataRow("timeout", TestStates.Timeout)]
    [DataRow("cancelled", TestStates.Cancelled)]
    public async Task ConsumeAsync_UnsuccessfulStatesForwardExceptionChainAndOnlyFailuresCarryDiff(string outcome, byte expectedState)
    {
        using DotnetTestConnectionTests.ConnectionFixture fixture = DotnetTestConnectionTests.CreateConnection(TestContext.CancellationToken);
        List<TestResultMessages> sent = [];
        fixture.Client.RespondVoid<TestResultMessages>(TestContext.CancellationToken, sent.Add);
        var exception = new InvalidOperationException("outer", new ArgumentException("inner"));
#pragma warning disable CS0618, MTP0001 // Exercise the still-supported cancelled wire outcome.
        TestNodeStateProperty state = outcome switch
        {
            "failed" => new FailedTestNodeStateProperty(exception, "reason"),
            "error" => new ErrorTestNodeStateProperty(exception, "reason"),
            "timeout" => new TimeoutTestNodeStateProperty(exception, "reason"),
            _ => new CancelledTestNodeStateProperty(exception, "reason"),
        };
#pragma warning restore CS0618, MTP0001
        var consumer = new DotnetTestDataConsumer(fixture.Connection, fixture.Environment.Object);

        await consumer.ConsumeAsync(Mock.Of<IDataProducer>(), CreateUpdate(
            state, FixedTiming(), new StandardOutputProperty("out"), new StandardErrorProperty("err"),
            new AssertionFailureProperty("expected", "actual"), new RetryAttemptProperty(2, false)), TestContext.CancellationToken);

        TestResultMessages batch = Assert.ContainsSingle(sent);
        Assert.IsEmpty(batch.SuccessfulTestMessages);
        FailedTestResultMessage result = Assert.ContainsSingle(batch.FailedTestMessages);
        Assert.AreEqual(expectedState, result.State);
        Assert.AreEqual(123L, result.Duration);
        Assert.AreEqual("reason", result.Reason);
        Assert.AreEqual("uid", result.Uid);
        Assert.AreEqual("DisplayName", result.DisplayName);
        Assert.AreEqual("session", result.SessionUid);
        Assert.AreEqual("out", result.StandardOutput);
        Assert.AreEqual("err", result.ErrorOutput);
        Assert.AreEqual(2, result.RetryAttemptNumber);
        Assert.IsFalse(result.IsSuperseded);
        string? expectedDiff = outcome == "failed" ? "expected" : null;
        string? expectedActualText = outcome == "failed" ? "actual" : null;
        Assert.AreEqual(expectedDiff, result.Expected);
        Assert.AreEqual(expectedActualText, result.Actual);
        Assert.IsNotNull(result.Exceptions);
        Assert.AreSequenceEqual(["reason", "inner"], result.Exceptions.Select(value => value.ErrorMessage));
        Assert.AreSequenceEqual(["System.InvalidOperationException", "System.ArgumentException"], result.Exceptions.Select(value => value.ErrorType));
    }

    [TestMethod]
    public async Task ConsumeAsync_CompletionOverridesFailureAndArtifactsFollowDiscoveryInOrder()
    {
        using DotnetTestConnectionTests.ConnectionFixture fixture = DotnetTestConnectionTests.CreateConnection(TestContext.CancellationToken);
        List<object> sent = [];
        fixture.Client.RespondVoid<DiscoveredTestMessages>(TestContext.CancellationToken, value => sent.Add(value));
        fixture.Client.RespondVoid<FileArtifactMessages>(TestContext.CancellationToken, value => sent.Add(value));
        var first = new FileInfo(Path.Combine(Path.GetTempPath(), "first-consumer-artifact.txt"));
        var second = new FileInfo(Path.Combine(Path.GetTempPath(), "second-consumer-artifact.txt"));
        var consumer = new DotnetTestDataConsumer(fixture.Connection, fixture.Environment.Object);

        await consumer.ConsumeAsync(Mock.Of<IDataProducer>(), CreateUpdate(
            new FailedTestNodeStateProperty("failure"), TestNodeExecutionCompletedProperty.CachedInstance,
            new FileArtifactProperty(first, "first"), new FileArtifactProperty(second, "second", "detail")),
            TestContext.CancellationToken);

        Assert.HasCount(3, sent);
        DiscoveredTestMessages discovery = Assert.IsInstanceOfType<DiscoveredTestMessages>(sent[0]);
        Assert.AreEqual("uid", Assert.ContainsSingle(discovery.DiscoveredMessages).Uid);
        FileArtifactMessage secondArtifact = Assert.ContainsSingle(Assert.IsInstanceOfType<FileArtifactMessages>(sent[1]).FileArtifacts);
        FileArtifactMessage firstArtifact = Assert.ContainsSingle(Assert.IsInstanceOfType<FileArtifactMessages>(sent[2]).FileArtifacts);
        Assert.AreEqual(first.FullName, firstArtifact.FullPath);
        Assert.AreEqual("first", firstArtifact.DisplayName);
        Assert.AreEqual(string.Empty, firstArtifact.Description);
        Assert.AreEqual(second.FullName, secondArtifact.FullPath);
        Assert.AreEqual("detail", secondArtifact.Description);
        foreach (FileArtifactMessage artifact in new[] { firstArtifact, secondArtifact })
        {
            Assert.AreEqual("uid", artifact.TestUid);
            Assert.AreEqual("DisplayName", artifact.TestDisplayName);
            Assert.AreEqual("session", artifact.SessionUid);
            Assert.IsNull(artifact.Kind);
        }
    }

    [TestMethod]
    public async Task ConsumeAsync_SessionAndUnscopedArtifactsPreserveWireIdentityWithoutAccessingFiles()
    {
        using DotnetTestConnectionTests.ConnectionFixture fixture = DotnetTestConnectionTests.CreateConnection(TestContext.CancellationToken);
        List<FileArtifactMessages> sent = [];
        fixture.Client.RespondVoid<FileArtifactMessages>(TestContext.CancellationToken, sent.Add);
        var consumer = new DotnetTestDataConsumer(fixture.Connection, fixture.Environment.Object);
        var file = new FileInfo(Path.Combine(Path.GetTempPath(), "consumer-report.trx"));

        await consumer.ConsumeAsync(Mock.Of<IDataProducer>(), new SessionFileArtifact(new SessionUid("session"), file, "report", "description", "microsoft.testing.trx"), TestContext.CancellationToken);
        await consumer.ConsumeAsync(Mock.Of<IDataProducer>(), new FileArtifact(file, "unscoped"), TestContext.CancellationToken);

        Assert.HasCount(2, sent);
        Assert.AreEqual("execution", sent[0].ExecutionId);
        Assert.AreEqual(DotnetTestConnection.InstanceId, sent[0].InstanceId);
        FileArtifactMessage scoped = Assert.ContainsSingle(sent[0].FileArtifacts);
        FileArtifactMessage unscoped = Assert.ContainsSingle(sent[1].FileArtifacts);
        Assert.AreEqual(file.FullName, scoped.FullPath);
        Assert.AreEqual("report", scoped.DisplayName);
        Assert.AreEqual("description", scoped.Description);
        Assert.AreEqual("session", scoped.SessionUid);
        Assert.AreEqual("microsoft.testing.trx", scoped.Kind);
        Assert.AreEqual(string.Empty, scoped.TestUid);
        Assert.AreEqual(string.Empty, scoped.TestDisplayName);
        Assert.AreEqual(file.FullName, unscoped.FullPath);
        Assert.AreEqual("unscoped", unscoped.DisplayName);
        Assert.AreEqual(string.Empty, unscoped.SessionUid);
        Assert.AreEqual(string.Empty, unscoped.Description);
        Assert.IsNull(unscoped.Kind);
    }

    [TestMethod]
    public async Task SessionCallbacks_SendStartAndEndWithOriginalApplicationToken()
    {
        using DotnetTestConnectionTests.ConnectionFixture fixture = DotnetTestConnectionTests.CreateConnection(TestContext.CancellationToken);
        List<TestSessionEvent> sent = [];
        fixture.Client.RespondVoid<TestSessionEvent>(TestContext.CancellationToken, sent.Add);
        var consumer = new DotnetTestDataConsumer(fixture.Connection, fixture.Environment.Object);
        var context = new Mock<ITestSessionContext>();
        context.SetupGet(value => value.SessionUid).Returns(new SessionUid("actual-session"));

        await consumer.OnTestSessionStartingAsync(context.Object);
        await consumer.OnTestSessionFinishingAsync(context.Object);

        Assert.AreSequenceEqual(
            [new TestSessionEvent(SessionEventTypes.TestSessionStart, "actual-session", "execution"), new TestSessionEvent(SessionEventTypes.TestSessionEnd, "actual-session", "execution")],
            sent);
        Assert.IsTrue(await consumer.IsEnabledAsync());
        Assert.AreSequenceEqual([typeof(TestNodeUpdateMessage), typeof(SessionFileArtifact), typeof(FileArtifact), typeof(TestCoverageMessage)], consumer.DataTypesConsumed);
    }

    [TestMethod]
    public async Task ConsumeAsync_OutcomeLessUpdateAndUnrelatedDataDoNotSendRequests()
    {
        using DotnetTestConnectionTests.ConnectionFixture fixture = DotnetTestConnectionTests.CreateConnection(TestContext.CancellationToken);
        var consumer = new DotnetTestDataConsumer(fixture.Connection, fixture.Environment.Object);

        await consumer.ConsumeAsync(Mock.Of<IDataProducer>(), CreateUpdate(new TestMetadataProperty("category", "value")), TestContext.CancellationToken);
        await consumer.ConsumeAsync(Mock.Of<IDataProducer>(), Mock.Of<IData>(), TestContext.CancellationToken);

        Assert.IsEmpty(fixture.Client.Mock.Invocations);
    }

    private static TestNodeUpdateMessage CreateUpdate(params IProperty[] properties)
        => new(new SessionUid("session"), new TestNode
        {
            Uid = new TestNodeUid("uid"),
            DisplayName = "DisplayName",
            Properties = new PropertyBag(properties),
        });

    private static TimingProperty FixedTiming()
    {
        DateTimeOffset start = new(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);
        return new TimingProperty(new TimingInfo(start, start.AddTicks(123), TimeSpan.FromTicks(123)));
    }

    [TestMethod]
    [DynamicData(nameof(DuplicateSingleOrDefaultProperties))]
    public void GetTestNodeDetails_WithDuplicateSingleOrDefaultProperty_ThrowsInvalidOperationException(Type propertyType, IProperty firstProperty, IProperty secondProperty)
    {
        TestNodeUpdateMessage testNodeUpdateMessage = new(new SessionUid("session"), new TestNode
        {
            Uid = new TestNodeUid("uid"),
            DisplayName = "DisplayName",
            Properties = new PropertyBag(PassedTestNodeStateProperty.CachedInstance, firstProperty, secondProperty),
        });

        MethodInfo getTestNodeDetails = typeof(DotnetTestDataConsumer).GetMethod("GetTestNodeDetails", BindingFlags.Static | BindingFlags.NonPublic)!;
        TargetInvocationException exception = Assert.ThrowsExactly<TargetInvocationException>(() => getTestNodeDetails.Invoke(null, [testNodeUpdateMessage]));

        InvalidOperationException invalidOperationException = Assert.IsInstanceOfType<InvalidOperationException>(exception.InnerException);
        Assert.AreEqual($"Found multiple properties of type '{propertyType}'.", invalidOperationException.Message);
    }

    public static IEnumerable<object[]> DuplicateSingleOrDefaultProperties()
    {
        yield return [typeof(TimingProperty), CreateTimingProperty(), CreateTimingProperty()];
        yield return [typeof(StandardOutputProperty), new StandardOutputProperty("first"), new StandardOutputProperty("second")];
        yield return [typeof(StandardErrorProperty), new StandardErrorProperty("first"), new StandardErrorProperty("second")];
        yield return [typeof(AssertionFailureProperty), new AssertionFailureProperty("first", null), new AssertionFailureProperty("second", null)];
        yield return [typeof(RetryAttemptProperty), new RetryAttemptProperty(1, true), new RetryAttemptProperty(2, false)];
    }

    [TestMethod]
    public void GetTestNodeDetails_WhenFailedWithAssertionFailureProperty_PopulatesExpectedAndActual()
    {
        // The supported channel: no exception is required for the diff to reach the SDK.
        DotnetTestDataConsumer.TestNodeDetails details = InvokeGetTestNodeDetails(
            [new FailedTestNodeStateProperty("Assert.AreEqual failed."), new AssertionFailureProperty("5", "2")]);

        Assert.AreEqual(TestStates.Failed, details.State);
        Assert.AreEqual("5", details.Expected);
        Assert.AreEqual("2", details.Actual);
    }

    [TestMethod]
    public void GetTestNodeDetails_WhenFailedWithAssertionFailureProperty_PrefersPropertyOverExceptionData()
    {
        var exception = new InvalidOperationException("boom");
        exception.Data["assert.expected"] = "legacy expected";
        exception.Data["assert.actual"] = "legacy actual";

        DotnetTestDataConsumer.TestNodeDetails details = InvokeGetTestNodeDetails(
            [new FailedTestNodeStateProperty(exception), new AssertionFailureProperty("5", "2")]);

        Assert.AreEqual("5", details.Expected);
        Assert.AreEqual("2", details.Actual);
    }

    [TestMethod]
    public void GetTestNodeDetails_WhenAssertionFailurePropertyIsOneSided_DoesNotSpliceInExceptionData()
    {
        // The property is authoritative as a whole: a half taken from the property and the other half from a
        // stale Exception.Data entry would render a diff that never existed.
        var exception = new InvalidOperationException("boom");
        exception.Data["assert.expected"] = "legacy expected";
        exception.Data["assert.actual"] = "legacy actual";

        DotnetTestDataConsumer.TestNodeDetails details = InvokeGetTestNodeDetails(
            [new FailedTestNodeStateProperty(exception), new AssertionFailureProperty("5", null)]);

        Assert.AreEqual("5", details.Expected);
        Assert.IsNull(details.Actual);
    }

    [TestMethod]
    public void GetTestNodeDetails_WhenFailedWithAssertData_PopulatesExpectedAndActual()
    {
        // Exercises the production extraction path (Exception.Data["assert.expected"/"assert.actual"]) that the
        // serializer round-trip tests bypass by constructing FailedTestResultMessage directly. Without this a
        // regression in the producer would leave every forwarded pipe payload null while those tests still pass.
        var exception = new InvalidOperationException("boom");
        exception.Data["assert.expected"] = "the expected";
        exception.Data["assert.actual"] = "the actual";

        DotnetTestDataConsumer.TestNodeDetails details = InvokeGetTestNodeDetails(new FailedTestNodeStateProperty(exception));

        Assert.AreEqual(TestStates.Failed, details.State);
        Assert.AreEqual("the expected", details.Expected);
        Assert.AreEqual("the actual", details.Actual);
    }

    [TestMethod]
    public void GetTestNodeDetails_WhenPassed_DoesNotPopulateExpectedOrActual()
    {
        DotnetTestDataConsumer.TestNodeDetails details = InvokeGetTestNodeDetails(PassedTestNodeStateProperty.CachedInstance);

        Assert.AreEqual(TestStates.Passed, details.State);
        Assert.IsNull(details.Expected);
        Assert.IsNull(details.Actual);
    }

    [TestMethod]
    public void GetTestNodeDetails_WhenExecutionCompleted_ReturnsDiscoveredWithoutOutcome()
    {
        DotnetTestDataConsumer.TestNodeDetails details = InvokeGetTestNodeDetails(TestNodeExecutionCompletedProperty.CachedInstance);

        Assert.AreEqual(TestStates.Discovered, details.State);
        Assert.IsNull(details.Duration);
        Assert.IsNull(details.Exceptions);
        Assert.IsNull(details.Expected);
        Assert.IsNull(details.Actual);
    }

    [TestMethod]
    public void GetTestNodeDetails_WhenErroredWithAssertData_DoesNotPopulateExpectedOrActual()
    {
        // Only failed tests carry the assertion diff, mirroring TerminalOutputDevice's single-assembly behavior;
        // error/timeout/cancelled states must not surface expected/actual even when their exception carries the data.
        var exception = new InvalidOperationException("boom");
        exception.Data["assert.expected"] = "the expected";
        exception.Data["assert.actual"] = "the actual";

        DotnetTestDataConsumer.TestNodeDetails details = InvokeGetTestNodeDetails(new ErrorTestNodeStateProperty(exception));

        Assert.AreEqual(TestStates.Error, details.State);
        Assert.IsNull(details.Expected);
        Assert.IsNull(details.Actual);
    }

    [TestMethod]
    public void GetTestNodeDetails_WhenSupersededRetryAttemptFailed_PopulatesRetryAttemptNumberAndIsSuperseded()
    {
        DotnetTestDataConsumer.TestNodeDetails details = InvokeGetTestNodeDetails(
            [new FailedTestNodeStateProperty("boom"), new RetryAttemptProperty(1, isSuperseded: true)]);

        Assert.AreEqual(TestStates.Failed, details.State);
        Assert.AreEqual(1, details.RetryAttemptNumber);
        Assert.IsTrue(details.IsSuperseded);
    }

    [TestMethod]
    public void GetTestNodeDetails_WhenFinalRetryAttemptPassed_PopulatesRetryAttemptNumberAndIsSupersededFalse()
    {
        DotnetTestDataConsumer.TestNodeDetails details = InvokeGetTestNodeDetails(
            [PassedTestNodeStateProperty.CachedInstance, new RetryAttemptProperty(2, isSuperseded: false)]);

        Assert.AreEqual(TestStates.Passed, details.State);
        Assert.AreEqual(2, details.RetryAttemptNumber);
        Assert.IsFalse(details.IsSuperseded);
    }

    [TestMethod]
    public void GetTestNodeDetails_WhenNoRetryAttemptProperty_DoesNotPopulateRetryAttemptNumberOrIsSuperseded()
    {
        DotnetTestDataConsumer.TestNodeDetails details = InvokeGetTestNodeDetails(PassedTestNodeStateProperty.CachedInstance);

        Assert.IsNull(details.RetryAttemptNumber);
        Assert.IsNull(details.IsSuperseded);
    }

    [TestMethod]
    public void CreateFileArtifactMessages_FromSessionFileArtifact_PreservesKind()
    {
        // Exercises the producer-to-wire mapping the serializer round-trip tests bypass (they start from
        // an already-populated FileArtifactMessage). Without this a regression that dropped the Kind here
        // would break post-processing grouping while every serializer test stayed green.
        var artifact = new SessionFileArtifact(new SessionUid("session"), new FileInfo("/path/a.trx"), "a.trx", "desc", "microsoft.testing.trx");

        FileArtifactMessages messages = DotnetTestDataConsumer.CreateFileArtifactMessages("exec-1", artifact);

        Assert.AreEqual("exec-1", messages.ExecutionId);
        Assert.HasCount(1, messages.FileArtifacts);
        Assert.AreEqual("microsoft.testing.trx", messages.FileArtifacts[0].Kind);
        Assert.AreEqual("session", messages.FileArtifacts[0].SessionUid);
    }

    [TestMethod]
    public void CreateFileArtifactMessages_FromSessionFileArtifactWithoutKind_SendsNullKind()
    {
        var artifact = new SessionFileArtifact(new SessionUid("session"), new FileInfo("/path/a.txt"), "a.txt");

        FileArtifactMessages messages = DotnetTestDataConsumer.CreateFileArtifactMessages("exec-1", artifact);

        Assert.IsNull(messages.FileArtifacts[0].Kind);
    }

    [TestMethod]
    public void CreateFileArtifactMessages_FromFileArtifact_PreservesKind()
    {
        var artifact = new FileArtifact(new FileInfo("/path/a.trx"), "a.trx", "desc", "microsoft.testing.trx");

        FileArtifactMessages messages = DotnetTestDataConsumer.CreateFileArtifactMessages("exec-1", artifact);

        Assert.HasCount(1, messages.FileArtifacts);
        Assert.AreEqual("microsoft.testing.trx", messages.FileArtifacts[0].Kind);
    }

    [TestMethod]
    public void CreateFileArtifactMessages_FromFileArtifactWithoutKind_SendsNullKind()
    {
        var artifact = new FileArtifact(new FileInfo("/path/a.txt"), "a.txt");

        FileArtifactMessages messages = DotnetTestDataConsumer.CreateFileArtifactMessages("exec-1", artifact);

        Assert.IsNull(messages.FileArtifacts[0].Kind);
    }

    private static DotnetTestDataConsumer.TestNodeDetails InvokeGetTestNodeDetails(IProperty stateProperty)
        => InvokeGetTestNodeDetails([stateProperty]);

    private static DotnetTestDataConsumer.TestNodeDetails InvokeGetTestNodeDetails(IProperty[] properties)
    {
        TestNodeUpdateMessage testNodeUpdateMessage = new(new SessionUid("session"), new TestNode
        {
            Uid = new TestNodeUid("uid"),
            DisplayName = "DisplayName",
            Properties = new PropertyBag(properties),
        });

        MethodInfo getTestNodeDetails = typeof(DotnetTestDataConsumer).GetMethod("GetTestNodeDetails", BindingFlags.Static | BindingFlags.NonPublic)!;
        return Assert.IsInstanceOfType<DotnetTestDataConsumer.TestNodeDetails>(getTestNodeDetails.Invoke(null, [testNodeUpdateMessage]));
    }

    private static TimingProperty CreateTimingProperty()
        => new(new TimingInfo(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1)));
}
