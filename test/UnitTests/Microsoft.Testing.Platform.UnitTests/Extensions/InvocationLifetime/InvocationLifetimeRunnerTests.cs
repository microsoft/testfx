// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.InvocationLifetime;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests.Extensions.InvocationLifetime;

#pragma warning disable TPEXP // Invocation lifetime handlers are experimental.

[TestClass]
public sealed class InvocationLifetimeRunnerTests
{
    private const string OwnerVariable = EnvironmentVariableConstants.TESTINGPLATFORM_INVOCATIONLIFETIME_OWNER;
    private readonly List<string> _calls = [];
    private readonly Dictionary<string, string?> _variables = [];
    private readonly Mock<IEnvironment> _environment = new();
    private readonly Mock<IOutputDevice> _outputDevice = new();

    public InvocationLifetimeRunnerTests()
    {
        _environment.SetupGet(e => e.ProcessId).Returns(42);
        _environment.SetupGet(e => e.NewLine).Returns("\n");
        _environment.Setup(e => e.GetEnvironmentVariable(It.IsAny<string>()))
            .Returns((string name) => _variables.TryGetValue(name, out string? value) ? value : null);
        _environment.Setup(e => e.SetEnvironmentVariable(It.IsAny<string>(), It.IsAny<string?>()))
            .Callback((string name, string? value) =>
            {
                if (value is null)
                {
                    _variables.Remove(name);
                }
                else
                {
                    _variables[name] = value;
                }
            });
    }

    [TestMethod]
    public async Task Setup_RunsInOrder_AndCleanupRunsInReverseOrder()
    {
        InvocationLifetimeRunner runner = CreateRunner([new RecordingHandler("a", _calls), new RecordingHandler("b", _calls)]);

        Assert.IsNull(await runner.RunSetupAsync(CancellationToken.None));
        int exitCode = await runner.RunCleanupAsync((int)ExitCode.AtLeastOneTestFailed, isCanceled: false);

        Assert.AreEqual((int)ExitCode.AtLeastOneTestFailed, exitCode);
        Assert.AreSequenceEqual(["setup:a", "setup:b", "cleanup:b(2,False)", "cleanup:a(2,False)", "dispose:a", "dispose:b"], _calls);
    }

    [TestMethod]
    public async Task SetupFailure_SkipsRemainingSetups_AndCleansUpEnteredHandlers()
    {
        InvocationLifetimeRunner runner = CreateRunner(
        [
            new RecordingHandler("a", _calls),
            new RecordingHandler("b", _calls) { SetupException = new InvalidOperationException("boom") },
            new RecordingHandler("c", _calls),
        ], setOwnerMarker: true);

        int? setupExitCode = await runner.RunSetupAsync(CancellationToken.None);
        int exitCode = await runner.RunCleanupAsync(setupExitCode!.Value, isCanceled: false);

        Assert.AreEqual((int)ExitCode.GenericFailure, setupExitCode);
        Assert.AreEqual((int)ExitCode.GenericFailure, exitCode);
        Assert.AreSequenceEqual(["setup:a", "setup:b", "cleanup:b(1,False)", "cleanup:a(1,False)", "dispose:a", "dispose:b", "dispose:c"], _calls);
        _outputDevice.Verify(
            d => d.DisplayAsync(runner, It.Is<ErrorMessageOutputDeviceData>(m => m.Message.Contains("'b'") && m.Message.Contains("boom")), It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.IsFalse(_variables.ContainsKey(OwnerVariable));
    }

    [TestMethod]
    public async Task SetupCanceled_ReturnsTestSessionAborted_WithoutReportingAnError()
    {
        using CancellationTokenSource cts = new();
        InvocationLifetimeRunner runner = CreateRunner([new RecordingHandler("a", _calls) { OnSetup = cts.Cancel }]);

        Assert.AreEqual((int)ExitCode.TestSessionAborted, await runner.RunSetupAsync(cts.Token));
        Assert.AreEqual((int)ExitCode.TestSessionAborted, await runner.RunCleanupAsync((int)ExitCode.TestSessionAborted, isCanceled: true));
        Assert.Contains("cleanup:a(3,True)", _calls);
        _outputDevice.Verify(d => d.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task SetupCanceledByHandlerReturningNormally_DoesNotEnterRemainingHandlers()
    {
        using CancellationTokenSource cts = new();
        InvocationLifetimeRunner runner = CreateRunner(
            [new SilentlyCancelingHandler(cts), new RecordingHandler("b", _calls)],
            setOwnerMarker: true);

        Assert.AreEqual((int)ExitCode.TestSessionAborted, await runner.RunSetupAsync(cts.Token));
        Assert.IsFalse(_variables.ContainsKey(OwnerVariable));

        await runner.RunCleanupAsync((int)ExitCode.TestSessionAborted, isCanceled: true);
        Assert.AreSequenceEqual(["dispose:b"], _calls);
    }

    [TestMethod]
    public async Task Cleanup_ReportsCanceledFailureAndAbortedExitCodeAsTestSessionAborted()
    {
        InvocationLifetimeRunner runner = CreateRunner([new RecordingHandler("a", _calls)]);
        await runner.RunSetupAsync(CancellationToken.None);

        Assert.AreEqual((int)ExitCode.TestSessionAborted, await runner.RunCleanupAsync((int)ExitCode.GenericFailure, isCanceled: true));
        Assert.Contains("cleanup:a(3,True)", _calls);
    }

    [TestMethod]
    public async Task Cleanup_TreatsTestSessionAbortedExitCodeAsCanceled()
    {
        InvocationLifetimeRunner runner = CreateRunner([new RecordingHandler("a", _calls)]);
        await runner.RunSetupAsync(CancellationToken.None);

        await runner.RunCleanupAsync((int)ExitCode.TestSessionAborted, isCanceled: false);
        Assert.Contains("cleanup:a(3,True)", _calls);
    }

    [TestMethod]
    public async Task ReleaseIfNotStarted_DisposesWithoutCleanup()
    {
        InvocationLifetimeRunner runner = CreateRunner([new RecordingHandler("a", _calls)]);

        await runner.ReleaseIfNotStartedAsync();

        Assert.AreSequenceEqual(["dispose:a"], _calls);
        Assert.IsTrue(runner.IsCleanupCompleted);
    }

    [TestMethod]
    public async Task ReleaseIfNotStarted_IsNoOpOnceSetupStarted()
    {
        InvocationLifetimeRunner runner = CreateRunner([new RecordingHandler("a", _calls)]);
        await runner.RunSetupAsync(CancellationToken.None);

        await runner.ReleaseIfNotStartedAsync();

        Assert.AreSequenceEqual(["setup:a"], _calls);
        Assert.IsFalse(runner.IsCleanupCompleted);
    }

    [TestMethod]
    public async Task CleanupFailure_TurnsSuccessIntoGenericFailure_AndRunsOtherCleanups()
    {
        InvocationLifetimeRunner runner = CreateRunner(
        [
            new RecordingHandler("a", _calls),
            new RecordingHandler("b", _calls) { CleanupException = new InvalidOperationException("boom") },
        ]);

        await runner.RunSetupAsync(CancellationToken.None);

        Assert.AreEqual((int)ExitCode.GenericFailure, await runner.RunCleanupAsync((int)ExitCode.Success, isCanceled: false));
        Assert.Contains("cleanup:a(0,False)", _calls);
        Assert.Contains("dispose:b", _calls);
    }

    [TestMethod]
    public async Task CleanupFailure_DoesNotMaskEarlierFailure()
    {
        InvocationLifetimeRunner runner = CreateRunner([new RecordingHandler("a", _calls) { CleanupException = new InvalidOperationException("boom") }]);

        await runner.RunSetupAsync(CancellationToken.None);

        Assert.AreEqual((int)ExitCode.AtLeastOneTestFailed, await runner.RunCleanupAsync((int)ExitCode.AtLeastOneTestFailed, isCanceled: false));
    }

    [TestMethod]
    public async Task Cleanup_IsIdempotent_AndReturnsFirstResult()
    {
        InvocationLifetimeRunner runner = CreateRunner([new RecordingHandler("a", _calls)]);
        await runner.RunSetupAsync(CancellationToken.None);

        int first = await runner.RunCleanupAsync((int)ExitCode.AtLeastOneTestFailed, isCanceled: true);
        int second = await runner.RunCleanupAsync((int)ExitCode.Success, isCanceled: false);

        Assert.AreEqual((int)ExitCode.AtLeastOneTestFailed, first);
        Assert.AreEqual(first, second);
        Assert.IsTrue(runner.IsCleanupCompleted);
        Assert.AreSequenceEqual(["setup:a", "cleanup:a(2,True)", "dispose:a"], _calls);
    }

    [TestMethod]
    public async Task Cleanup_WithoutSetup_OnlyDisposesHandlers()
    {
        InvocationLifetimeRunner runner = CreateRunner([new RecordingHandler("a", _calls)]);

        Assert.AreEqual((int)ExitCode.Success, await runner.RunCleanupAsync((int)ExitCode.Success, isCanceled: false));
        Assert.AreSequenceEqual(["dispose:a"], _calls);
    }

    [TestMethod]
    public async Task Setup_CanOnlyRunOnce()
    {
        InvocationLifetimeRunner runner = CreateRunner([new RecordingHandler("a", _calls)]);
        await runner.RunSetupAsync(CancellationToken.None);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => runner.RunSetupAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task OwnerMarker_IsSetAfterSetup_AndRemovedOnCleanup()
    {
        string? markerSeenDuringSetup = "unset";
        RecordingHandler handler = new("a", _calls) { OnSetup = () => markerSeenDuringSetup = _variables.TryGetValue(OwnerVariable, out string? v) ? v : null };
        InvocationLifetimeRunner runner = CreateRunner([handler], setOwnerMarker: true);

        await runner.RunSetupAsync(CancellationToken.None);

        Assert.IsNull(markerSeenDuringSetup);
        Assert.AreEqual("42", _variables[OwnerVariable]);

        await runner.RunCleanupAsync((int)ExitCode.Success, isCanceled: false);

        Assert.IsFalse(_variables.ContainsKey(OwnerVariable));
    }

    [TestMethod]
    public async Task PublishedVariables_AreSetOnProcess_AndKeptWithoutRestore()
    {
        RecordingHandler handler = new("a", _calls) { OnSetupContext = context => context.SetEnvironmentVariable("MY_VAR", "value") };
        InvocationLifetimeRunner runner = CreateRunner([handler]);

        await runner.RunSetupAsync(CancellationToken.None);
        Assert.AreEqual("value", _variables["MY_VAR"]);

        await runner.RunCleanupAsync((int)ExitCode.Success, isCanceled: false);
        Assert.AreEqual("value", _variables["MY_VAR"]);
    }

    [TestMethod]
    public async Task PublishedVariables_AreRestoredToOriginalValues_WhenRestoreIsEnabled()
    {
        _variables["EXISTING"] = "original";
        RecordingHandler handler = new("a", _calls)
        {
            OnSetupContext = context =>
            {
                context.SetEnvironmentVariable("EXISTING", "first");
                context.SetEnvironmentVariable("EXISTING", "second");
                context.SetEnvironmentVariable("NEW", "value");
            },
        };
        InvocationLifetimeRunner runner = CreateRunner([handler], restoreEnvironment: true);

        await runner.RunSetupAsync(CancellationToken.None);
        Assert.AreEqual("second", _variables["EXISTING"]);

        await runner.RunCleanupAsync((int)ExitCode.Success, isCanceled: false);

        Assert.AreEqual("original", _variables["EXISTING"]);
        Assert.IsFalse(_variables.ContainsKey("NEW"));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("A=B")]
    [DataRow("A\0B")]
    [DataRow("TESTINGPLATFORM_INVOCATIONLIFETIME_X")]
    [DataRow("testingplatform_invocationlifetime_owner")]
    public async Task PublishingInvalidName_FailsSetup(string name)
    {
        RecordingHandler handler = new("a", _calls) { OnSetupContext = context => context.SetEnvironmentVariable(name, "value") };
        InvocationLifetimeRunner runner = CreateRunner([handler]);

        Assert.AreEqual((int)ExitCode.GenericFailure, await runner.RunSetupAsync(CancellationToken.None));
        Assert.IsEmpty(_variables);
    }

    private InvocationLifetimeRunner CreateRunner(IReadOnlyList<IInvocationLifetimeHandler> handlers, bool setOwnerMarker = false, bool restoreEnvironment = false)
        => new(handlers, _environment.Object, _outputDevice.Object, Mock.Of<ILogger>(), setOwnerMarker, restoreEnvironment);

    private sealed class RecordingHandler : IInvocationLifetimeHandler, IAsyncDisposable
    {
        private readonly List<string> _calls;

        public RecordingHandler(string uid, List<string> calls)
        {
            Uid = uid;
            _calls = calls;
        }

        public Exception? SetupException { get; set; }

        public Exception? CleanupException { get; set; }

        public Action? OnSetup { get; set; }

        public Action<InvocationStartingContext>? OnSetupContext { get; set; }

        public string Uid { get; }

        public string Version => "1.0.0";

        public string DisplayName => Uid;

        public string Description => Uid;

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public Task OnInvocationStartingAsync(InvocationStartingContext context, CancellationToken cancellationToken)
        {
            _calls.Add($"setup:{Uid}");
            OnSetup?.Invoke();
            OnSetupContext?.Invoke(context);
            cancellationToken.ThrowIfCancellationRequested();
            return SetupException is null ? Task.CompletedTask : Task.FromException(SetupException);
        }

        public Task OnInvocationCompletedAsync(InvocationCompletedContext context, CancellationToken cancellationToken)
        {
            _calls.Add($"cleanup:{Uid}({context.ExitCode},{context.IsCanceled})");
            return CleanupException is null ? Task.CompletedTask : Task.FromException(CleanupException);
        }

        public ValueTask DisposeAsync()
        {
            _calls.Add($"dispose:{Uid}");
            return default;
        }
    }

    private sealed class SilentlyCancelingHandler(CancellationTokenSource cancellationTokenSource) : IInvocationLifetimeHandler
    {
        public string Uid => "silent";

        public string Version => "1.0.0";

        public string DisplayName => Uid;

        public string Description => Uid;

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public Task OnInvocationStartingAsync(InvocationStartingContext context, CancellationToken cancellationToken)
        {
            cancellationTokenSource.Cancel();
            return Task.CompletedTask;
        }

        public Task OnInvocationCompletedAsync(InvocationCompletedContext context, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
