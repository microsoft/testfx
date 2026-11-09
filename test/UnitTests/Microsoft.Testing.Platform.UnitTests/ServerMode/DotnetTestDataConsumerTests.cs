// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;
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
    [DataRow("report.trx", true, true, DisplayName = "RetryArtifactManifest_Trx")]
    [DataRow("report.TRX", true, true, DisplayName = "RetryArtifactManifest_UppercaseTrx")]
    [DataRow("nested/report.trx", true, true, DisplayName = "RetryArtifactManifest_NestedTrx")]
    [DataRow("report.txt", true, false, DisplayName = "RetryArtifactManifest_OtherArtifact")]
    [DataRow("../attempt-sibling/report.trx", true, false, DisplayName = "RetryArtifactManifest_SiblingDirectory")]
    [DataRow("report.trx", false, false, DisplayName = "RetryArtifactManifest_NoOptIn")]
    public void TryDeferTrxArtifacts_OnlyDefersOwnedTrxAndPreservesWireIdentity(string relativePath, bool enabled, bool expectedDeferred)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"deferred-trx-{Guid.NewGuid():N}");
        string attempt = Path.Combine(directory, "attempt");
        string manifests = Path.Combine(directory, "manifests");
        Directory.CreateDirectory(attempt);
        try
        {
            string path = Path.GetFullPath(Path.Combine(attempt, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            var artifact = new FileArtifactMessage(path, "quoted \"report\"", "description", "test-uid", "test-name", "session", "microsoft.testing.trx");
            var message = new FileArtifactMessages("execution", "instance", [artifact]);

            bool deferred = DotnetTestDataConsumer.TryDeferTrxArtifacts(message, enabled ? attempt : null, manifests);

            Assert.AreEqual(expectedDeferred, deferred);
            if (!expectedDeferred)
            {
                Assert.IsFalse(Directory.Exists(manifests));
                return;
            }

            string manifest = Assert.ContainsSingle(Directory.GetFiles(manifests, "*.bin"));
            Assert.IsEmpty(Directory.GetFiles(manifests, "*.tmp"));
            FileArtifactMessages restored = DotnetTestDataConsumer.ReadDeferredTrxArtifacts(manifest);
            Assert.AreEqual("execution", restored.ExecutionId);
            Assert.AreEqual("instance", restored.InstanceId);
            Assert.AreEqual(artifact, Assert.ContainsSingle(restored.FileArtifacts));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    [DataRow(false, DisplayName = "RetryArtifactManifest_ControllerArtifact")]
    [DataRow(true, DisplayName = "RetryArtifactManifest_SessionArtifact")]
    public async Task ConsumeAsync_DeferredTrxIsRecordedInsteadOfSent(bool sessionScoped)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"deferred-trx-{Guid.NewGuid():N}");
        string attempt = Path.Combine(directory, "attempt");
        string manifests = Path.Combine(directory, "manifests");
        Directory.CreateDirectory(attempt);
        try
        {
            var environment = new Mock<IEnvironment>();
            environment.Setup(value => value.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_EXECUTIONID)).Returns("execution");
            environment.Setup(value => value.GetEnvironmentVariable(DotnetTestDataConsumer.DeferredTrxRootEnvironmentVariable)).Returns(attempt);
            environment.Setup(value => value.GetEnvironmentVariable(DotnetTestDataConsumer.DeferredTrxDirectoryEnvironmentVariable)).Returns(manifests);
            (DotnetTestConnection connection, Mock client) = CreateConnection(environment.Object);
            using DotnetTestConnection connectionOwnership = connection;
            var consumer = new DotnetTestDataConsumer(connection, environment.Object);
            var file = new FileInfo(Path.Combine(attempt, "report.trx"));
            IData artifact = sessionScoped
                ? new SessionFileArtifact(new SessionUid("session"), file, "report", "description", "microsoft.testing.trx")
                : new FileArtifact(file, "report", "description", "microsoft.testing.trx");

            await consumer.ConsumeAsync(Mock.Of<IDataProducer>(), artifact, TestContext.CancellationToken);

            Assert.IsEmpty(client.Invocations);
            FileArtifactMessages recorded = DotnetTestDataConsumer.ReadDeferredTrxArtifacts(Assert.ContainsSingle(Directory.GetFiles(manifests, "*.bin")));
            Assert.AreEqual("execution", recorded.ExecutionId);
            Assert.AreEqual(DotnetTestConnection.InstanceId, recorded.InstanceId);
            FileArtifactMessage result = Assert.ContainsSingle(recorded.FileArtifacts);
            Assert.AreEqual(file.FullName, result.FullPath);
            Assert.AreEqual(sessionScoped ? "session" : string.Empty, result.SessionUid);
            Assert.AreEqual("report", result.DisplayName);
            Assert.AreEqual("description", result.Description);
            Assert.AreEqual("microsoft.testing.trx", result.Kind);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ConsumeAsync_DeferredTestAttachmentStillStreamsTheTestResult()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"deferred-trx-{Guid.NewGuid():N}");
        string attempt = Path.Combine(directory, "attempt");
        string manifests = Path.Combine(directory, "manifests");
        Directory.CreateDirectory(attempt);
        try
        {
            var environment = new Mock<IEnvironment>();
            environment.Setup(value => value.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_EXECUTIONID)).Returns("execution");
            environment.Setup(value => value.GetEnvironmentVariable(DotnetTestDataConsumer.DeferredTrxRootEnvironmentVariable)).Returns(attempt);
            environment.Setup(value => value.GetEnvironmentVariable(DotnetTestDataConsumer.DeferredTrxDirectoryEnvironmentVariable)).Returns(manifests);
            (DotnetTestConnection connection, Mock client) = CreateConnection(environment.Object);
            using DotnetTestConnection connectionOwnership = connection;
            var consumer = new DotnetTestDataConsumer(connection, environment.Object);
            var file = new FileInfo(Path.Combine(attempt, "attachment.trx"));
            var update = new TestNodeUpdateMessage(new SessionUid("session"), new TestNode
            {
                Uid = new TestNodeUid("uid"),
                DisplayName = "DisplayName",
                Properties = new PropertyBag(PassedTestNodeStateProperty.CachedInstance, new FileArtifactProperty(file, "attachment")),
            });

            await consumer.ConsumeAsync(Mock.Of<IDataProducer>(), update, TestContext.CancellationToken);

            object request = Assert.ContainsSingle(client.Invocations).Arguments[0];
            TestResultMessages results = Assert.IsInstanceOfType<TestResultMessages>(request);
            Assert.AreEqual("uid", Assert.ContainsSingle(results.SuccessfulTestMessages).Uid);
            FileArtifactMessage recorded = Assert.ContainsSingle(DotnetTestDataConsumer.ReadDeferredTrxArtifacts(
                Assert.ContainsSingle(Directory.GetFiles(manifests, "*.bin"))).FileArtifacts);
            Assert.AreEqual(file.FullName, recorded.FullPath);
            Assert.AreEqual("uid", recorded.TestUid);
            Assert.AreEqual("DisplayName", recorded.TestDisplayName);
            Assert.AreEqual("session", recorded.SessionUid);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void TryDeferTrxArtifacts_WriteFailureIsNotReportedAsSuccess()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"deferred-trx-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string blocked = Path.Combine(directory, "blocked");
            File.WriteAllText(blocked, "not a directory");
            var message = new FileArtifactMessages("execution", "instance",
                [new FileArtifactMessage(Path.Combine(directory, "report.trx"), "report", null, null, null, null, null)]);

            Assert.ThrowsExactly<IOException>(() => DotnetTestDataConsumer.TryDeferTrxArtifacts(message, directory, blocked));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private (DotnetTestConnection Connection, Mock Client) CreateConnection(IEnvironment environment)
    {
        ITestApplicationModuleInfo module = Mock.Of<ITestApplicationModuleInfo>();
        var commandLine = new CommandLineHandler(new CommandLineParseResult(null, [], []), [], [], module, Mock.Of<IRuntimeFeature>());
        var cancellation = new Mock<ITestApplicationCancellationTokenSource>();
        cancellation.SetupGet(value => value.CancellationToken).Returns(TestContext.CancellationToken);
        var connection = new DotnetTestConnection(commandLine, environment, module, cancellation.Object);
        // The core's embedded transport interface is distinct from this assembly's linked IPC copy.
        FieldInfo transport = typeof(DotnetTestConnection).GetField("_transportClient", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var client = (Mock)Activator.CreateInstance(typeof(Mock<>).MakeGenericType(transport.FieldType))!;
        transport.SetValue(connection, client.Object);
        return (connection, client);
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
