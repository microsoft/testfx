// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Policy;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;

using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

using Moq;

using TestSessionContext = Microsoft.Testing.Platform.Services.TestSessionContext;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class RetryPreparationBehaviorTests
{
    [TestMethod]
    public async Task DirectoryPreparation_CollisionsExhaustTenProbesWithoutCreating()
    {
        using TrxBehaviorFixture fixture = CreateRetryFixture();
        List<string> probes = [];
        fixture.FileSystem.Setup(value => value.ExistDirectory(It.IsAny<string>()))
            .Callback<string>(probes.Add)
            .Returns(true);

        IOException exception = await Assert.ThrowsExactlyAsync<IOException>(
            () => new RetryOrchestrator(fixture.Services).OrchestrateTestHostExecutionAsync(CancellationToken.None));

        Assert.AreEqual(
            string.Format(
            CultureInfo.InvariantCulture, Policy.Resources.ExtensionResources.FailedToCreateRetryDirectoryBecauseOfCollision, fixture.Root), exception.Message);
        Assert.HasCount(10, probes);
        AssertCandidateRoots(fixture.Root, probes);
        fixture.FileSystem.Verify(value => value.CreateDirectory(It.IsAny<string>()), Times.Never);
        Assert.IsEmpty(fixture.Logs, "All collision checks precede transport construction.");
        Assert.IsEmpty(fixture.Publications);
    }

    [TestMethod]
    public async Task DirectoryPreparation_RethrowsLastIoFailureAfterTenAttempts()
    {
        using TrxBehaviorFixture fixture = CreateRetryFixture();
        fixture.FileSystem.Setup(value => value.ExistDirectory(It.IsAny<string>())).Returns(false);
        IOException[] failures = [.. Enumerable.Range(1, 10).Select(index => new IOException("creation-" + index))];
        List<string> created = [];
        fixture.FileSystem.Setup(value => value.CreateDirectory(It.IsAny<string>()))
            .Returns<string>(path =>
            {
                created.Add(path);
                throw failures[created.Count - 1];
            });

        IOException exception = await Assert.ThrowsExactlyAsync<IOException>(
            () => new RetryOrchestrator(fixture.Services).OrchestrateTestHostExecutionAsync(CancellationToken.None));

        Assert.AreSame(failures[9], exception);
        Assert.HasCount(10, created);
        AssertCandidateRoots(fixture.Root, created);
        fixture.FileSystem.Verify(value => value.ExistDirectory(It.IsAny<string>()), Times.Exactly(10));
        Assert.IsEmpty(fixture.Logs);
    }

    [TestMethod]
    public async Task DirectoryPreparation_LastIoFailureSurvivesLaterCollisions()
    {
        using TrxBehaviorFixture fixture = CreateRetryFixture();
        int probe = 0;
        var original = new IOException("first creation denied");
        fixture.FileSystem.Setup(value => value.ExistDirectory(It.IsAny<string>()))
            .Returns(() => ++probe != 1);
        fixture.FileSystem.Setup(value => value.CreateDirectory(It.IsAny<string>())).Throws(original);

        IOException exception = await Assert.ThrowsExactlyAsync<IOException>(
            () => new RetryOrchestrator(fixture.Services).OrchestrateTestHostExecutionAsync(CancellationToken.None));

        Assert.AreSame(original, exception, "Later collisions must not erase the actionable creation error.");
        Assert.AreEqual(10, probe);
        fixture.FileSystem.Verify(value => value.CreateDirectory(It.IsAny<string>()), Times.Once);
    }

    [TestMethod]
    public async Task DirectoryPreparation_TransientIoFailuresEventuallyCreateOneAttemptDirectory()
    {
        using TrxBehaviorFixture fixture = CreateRetryFixture();
        fixture.FileSystem.Setup(value => value.ExistDirectory(It.IsAny<string>())).Returns(false);
        List<string> created = [];
        fixture.FileSystem.Setup(value => value.CreateDirectory(It.IsAny<string>()))
            .Returns<string>(path =>
            {
                created.Add(path);
                return created.Count <= 2 ? throw new IOException("transient") : path;
            });
        var stopBeforeBinding = new InvalidOperationException("injected boundary before concrete pipe allocation");
        fixture.Logger.Setup(value => value.Log(
            LogLevel.Trace, It.IsAny<string>(), It.IsAny<Exception>(), It.IsAny<Func<string, Exception?, string>>()))
            .Throws(stopBeforeBinding);

        Exception exception = await Assert.ThrowsAsync<Exception>(
            () => new RetryOrchestrator(fixture.Services).OrchestrateTestHostExecutionAsync(CancellationToken.None));

        Assert.AreSame(stopBeforeBinding, exception);
        Assert.HasCount(4, created);
        AssertCandidateRoots(fixture.Root, created.Take(3));
        Assert.AreEqual(Path.Combine(created[2], "1"), created[3]);
        fixture.FileSystem.Verify(value => value.ExistDirectory(It.IsAny<string>()), Times.Exactly(3));
        Assert.IsFalse(fixture.Scheduler.HasWriter);
        Assert.IsEmpty(fixture.Publications);
    }

    [TestMethod]
    public async Task DirectoryPreparation_NonIoFailureIsNotRetried()
    {
        using TrxBehaviorFixture fixture = CreateRetryFixture();
        fixture.FileSystem.Setup(value => value.ExistDirectory(It.IsAny<string>())).Returns(false);
        var original = new UnauthorizedAccessException("not a transient collision");
        fixture.FileSystem.Setup(value => value.CreateDirectory(It.IsAny<string>())).Throws(original);

        UnauthorizedAccessException exception = await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(
            () => new RetryOrchestrator(fixture.Services).OrchestrateTestHostExecutionAsync(CancellationToken.None));

        Assert.AreSame(original, exception);
        fixture.FileSystem.Verify(value => value.ExistDirectory(It.IsAny<string>()), Times.Once);
        fixture.FileSystem.Verify(value => value.CreateDirectory(It.IsAny<string>()), Times.Once);
        Assert.IsEmpty(fixture.Logs);
    }

    [TestMethod]
    [DataRow(0, DisplayName = "RetryLifecycle_MissingPipeArgument")]
    [DataRow(1, DisplayName = "RetryLifecycle_EmptyPipeArguments")]
    [DataRow(2, DisplayName = "RetryLifecycle_MultiplePipeArguments")]
    public async Task Lifecycle_InvalidPipeArgumentsFailBeforeTransport(int input)
    {
        using TrxBehaviorFixture fixture = CreateRetryFixture();
        if (input != 0)
        {
            fixture.Options[RetryCommandLineOptionsProvider.RetryFailedTestsPipeNameOptionName] = input == 1 ? [] : ["one", "two"];
        }

        using var lifecycle = new RetryLifecycleCallbacks(fixture.Services);
        if (input == 0)
        {
#if NETCOREAPP
            System.Diagnostics.UnreachableException exception = await Assert.ThrowsExactlyAsync<System.Diagnostics.UnreachableException>(
                () => lifecycle.BeforeRunAsync(CancellationToken.None));
#else
            Exception exception = await Assert.ThrowsAsync<Exception>(
                () => lifecycle.BeforeRunAsync(CancellationToken.None));
            Assert.AreEqual("System.Diagnostics.UnreachableException", exception.GetType().FullName);
#endif
            Assert.Contains("unreachable", exception.Message);
        }
        else
        {
            ArgumentException exception = await Assert.ThrowsExactlyAsync<ArgumentException>(
                () => lifecycle.BeforeRunAsync(CancellationToken.None));
            Assert.AreEqual("pipeName", exception.ParamName);
            Assert.Contains("Pipe name expected", exception.Message);
        }

        Assert.IsNull(lifecycle.Client);
        Assert.IsNull(lifecycle.FailedTestsIDToRetry);
        Assert.IsEmpty(fixture.Logs);
        Assert.IsFalse(fixture.Scheduler.HasWriter);
    }

    [TestMethod]
    public async Task Lifecycle_LoggingFailurePreservesDiagnosticWithoutCreatingClient()
    {
        using TrxBehaviorFixture fixture = CreateRetryFixture();
        const string Pipe = "retry-pipe-with-spaces";
        fixture.Options[RetryCommandLineOptionsProvider.RetryFailedTestsPipeNameOptionName] = [Pipe];
        var original = new IOException("diagnostic failed before connection");
        fixture.Logger.Setup(value => value.LogAsync(
            LogLevel.Debug, It.IsAny<string>(), It.IsAny<Exception>(), It.IsAny<Func<string, Exception?, string>>()))
            .Returns(Task.FromException(original));
        using var lifecycle = new RetryLifecycleCallbacks(fixture.Services);

        IOException exception = await Assert.ThrowsExactlyAsync<IOException>(
            () => lifecycle.BeforeRunAsync(CancellationToken.None));

        Assert.AreSame(original, exception);
        fixture.Logger.Verify(
            value => value.LogAsync(
            LogLevel.Debug, $"Connecting to pipe '{Pipe}'", null, It.IsAny<Func<string, Exception?, string>>()), Times.Once);
        Assert.IsNull(lifecycle.Client);
        Assert.IsNull(lifecycle.FailedTestsIDToRetry);
    }

    [TestMethod]
    public async Task Lifecycle_DisabledCallbacksDoNotInspectTransport()
    {
        using TrxBehaviorFixture fixture = CreateRetryFixture();
        using var lifecycle = new RetryLifecycleCallbacks(fixture.Services);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.IsFalse(await lifecycle.IsEnabledAsync());
        await lifecycle.AfterRunAsync(17, cancellation.Token);
        fixture.Options[RetryCommandLineOptionsProvider.RetryFailedTestsPipeNameOptionName] = ["not-connected"];
        Assert.IsTrue(await lifecycle.IsEnabledAsync());
        await lifecycle.AfterRunAsync(0, cancellation.Token);
        lifecycle.Dispose();

        Assert.IsNull(lifecycle.Client);
        Assert.IsEmpty(fixture.Logs);
        fixture.FileSystem.VerifyNoOtherCalls();
    }

    [TestMethod]
    [DataRow("source-only")]
    [DataRow("destination-only")]
    [DataRow("primary-sibling")]
    [DataRow("diagnostic-sibling")]
    [DataRow("both-outside")]
    public async Task Consumer_OutsideMappedRootsWarnsBeforeTransport(string mapping)
    {
        using TrxBehaviorFixture fixture = CreateRetryFixture();
        string source = Path.Combine(fixture.Root, "host");
        string destination = Path.Combine(fixture.Root, "controller");
        string diagnostic = Path.Combine(fixture.Root, "diagnostics");
        if (mapping != "destination-only")
        {
            fixture.EnvironmentValues["TESTINGPLATFORM_ARTIFACT_PATH_SOURCE_ROOT"] = source;
        }

        if (mapping != "source-only")
        {
            fixture.EnvironmentValues["TESTINGPLATFORM_ARTIFACT_PATH_DESTINATION_ROOT"] = destination;
        }

        if (mapping is "diagnostic-sibling" or "both-outside")
        {
            fixture.EnvironmentValues["TESTINGPLATFORM_DIAGNOSTIC_ARTIFACT_PATH_SOURCE_ROOT"] = diagnostic;
            fixture.EnvironmentValues["TESTINGPLATFORM_DIAGNOSTIC_ARTIFACT_PATH_DESTINATION_ROOT"] = destination;
        }

        string path = mapping == "diagnostic-sibling"
            ? Path.Combine(diagnostic + "-sibling", "report.trx")
            : Path.Combine(source + "-sibling", "report.trx");
        var consumer = new RetryDataConsumer(fixture.Services);

        await consumer.ConsumeAsync(null!, new SessionFileArtifact(new SessionUid("session"), new FileInfo(path), "TRX", "report", "microsoft.testing.trx"), CancellationToken.None);

        (LogLevel level, string message, Exception? error) = Assert.ContainsSingle(fixture.Logs);
        Assert.AreEqual(LogLevel.Warning, level);
        Assert.AreEqual($"Ignoring retry artifact '{path}' because it is outside the configured AppContainer artifact roots.", message);
        Assert.IsEmpty(fixture.Publications);
        fixture.FileSystem.VerifyNoOtherCalls();
    }

    [TestMethod]
    [DataRow("no-state")]
    [DataRow("discovered")]
    [DataRow("in-progress")]
    [DataRow("superseded-failure")]
    public async Task Consumer_NonterminalUpdatesDoNotRequireInitializedTransport(string state)
    {
        using TrxBehaviorFixture fixture = CreateRetryFixture();
        PropertyBag properties = state switch
        {
            "discovered" => new(DiscoveredTestNodeStateProperty.CachedInstance),
            "in-progress" => new(InProgressTestNodeStateProperty.CachedInstance),
            "superseded-failure" => new(new FailedTestNodeStateProperty(), new RetryAttemptProperty(1, isSuperseded: true)),
            _ => new(),
        };
        var consumer = new RetryDataConsumer(fixture.Services);
        await consumer.InitializeAsync();
        await consumer.OnTestSessionStartingAsync(new TestSessionContext(CancellationToken.None));

        await consumer.ConsumeAsync(null!, new TestNodeUpdateMessage(new SessionUid("session"), new TestNode
        {
            Uid = "test",
            DisplayName = "ignored update",
            Properties = properties,
        }), CancellationToken.None);

        Assert.IsEmpty(fixture.Logs);
        Assert.IsEmpty(fixture.Publications);
        fixture.FileSystem.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task Consumer_UninitializedSessionCannotPublishInventedCounts()
    {
        using TrxBehaviorFixture fixture = CreateRetryFixture();
        var consumer = new RetryDataConsumer(fixture.Services);

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => consumer.OnTestSessionFinishingAsync(new TestSessionContext(CancellationToken.None)));

        Assert.Contains("Unexpected state", exception.Message);
        Assert.IsEmpty(fixture.Logs);
        Assert.IsEmpty(fixture.Publications);
    }

    private static TrxBehaviorFixture CreateRetryFixture()
    {
        var fixture = new TrxBehaviorFixture();
        fixture.Options[RetryCommandLineOptionsProvider.RetryFailedTestsOptionName] = ["2"];
        fixture.Module.Setup(value => value.GetCurrentExecutableInfo())
            .Returns(new ExecutableInfo("not-launched.exe", ["--retry-failed-tests", "2"], fixture.Root));
        return fixture;
    }

    private static void AssertCandidateRoots(string resultRoot, IEnumerable<string> paths)
    {
        foreach (string path in paths)
        {
            Assert.AreEqual(Path.Combine(resultRoot, "Retries"), Path.GetDirectoryName(path));
            Assert.AreEqual(5, Path.GetFileName(path).Length);
        }
    }
}
