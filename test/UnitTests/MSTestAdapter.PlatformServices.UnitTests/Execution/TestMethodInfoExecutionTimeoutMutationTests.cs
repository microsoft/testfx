// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

#if DEBUG && !NETFRAMEWORK
using Microsoft.Testing.TestInfrastructure;
#endif

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public sealed class TestMethodInfoExecutionTimeoutMutationTests : TestContainer
{
    private const string TestMethodName = "TestMethod";

    public async Task InvokeAsyncReportsATestWhoseOriginalCancellationSourceWasCancelledEvenWhenCleanupReplacedIt()
    {
        using var harness = new TimeoutHarness(typeof(SelfCancelingFixture));
        harness.Services.MockThreadOperations
            .Setup(operations => operations.Execute(It.IsAny<Action>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns<Action, int, CancellationToken>((action, _, _) =>
            {
                action();
                return true;
            });

        // The cleanup that follows the cancelled test swaps in a fresh, uncancelled source: only the original one still tells.
        TestResult result = await harness.Subject.InvokeAsync(null);

        result.Outcome.Should().Be(UnitTestOutcome.Timeout);
        result.TestFailureException.Should().BeOfType<TestFailedException>().Which.Message.Should().Be(
            string.Format(CultureInfo.CurrentCulture, Resource.Execution_Test_Cancelled, TestMethodName));
        harness.Context.CancellationTokenSource.IsCancellationRequested.Should().BeFalse();
    }

#if DEBUG && !NETFRAMEWORK
    public async Task InvokeAsyncAssertsThatTheMonitoredExecutionProducedAResult()
    {
        using var harness = new TimeoutHarness(typeof(SelfCancelingFixture));

        // The worker reports a normal completion without ever running the test, so there is no result to inspect.
        harness.Services.MockThreadOperations
            .Setup(operations => operations.Execute(It.IsAny<Action>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(true);
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);

        Func<Task> invoke = () => harness.Subject.InvokeAsync(null);

        await invoke.Should().ThrowAsync<NullReferenceException>();
        assertions.Should().Equal("result is not null");
    }
#endif

    public async Task InvokeAsyncCancelsTheTestCancellationSourceWhenTheTestRunIsCancelledWhileTheTestIsRunning()
    {
        using var harness = new TimeoutHarness(typeof(RunCancelingFixture));
        var runCancellation = new TestRunCancellationToken();
        harness.Services.TestRunCancellationToken = runCancellation;
        var workerCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Services.MockThreadOperations
            .Setup(operations => operations.Execute(It.IsAny<Action>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns<Action, int, CancellationToken>((action, timeout, token) => new ThreadOperations().Execute(
                () =>
                {
                    try
                    {
                        action();
                    }
                    finally
                    {
                        workerCompleted.TrySetResult(true);
                    }
                },
                timeout,
                token));
        using var releaseTest = new ManualResetEventSlim();
        RunCancelingFixture.ReleaseTest = releaseTest;
        CancellationTokenSource originalSource = harness.Context.CancellationTokenSource;

        try
        {
            // The test cancels the run and then blocks: the wait is abandoned long before the (distant) timeout elapses.
            TestResult result = await harness.Subject.InvokeAsync(null);

            result.Outcome.Should().Be(UnitTestOutcome.Timeout);
            result.TestFailureException.Should().BeOfType<TestFailedException>().Which.Message.Should().Be(
                string.Format(CultureInfo.CurrentCulture, Resource.Execution_Test_Cancelled, TestMethodName));
            originalSource.IsCancellationRequested.Should().BeTrue(because: "the cancellation of the run is propagated to the test that is still running");
        }
        finally
        {
            releaseTest.Set();
            await workerCompleted.Task;
        }
    }

    private sealed class TimeoutHarness : IDisposable
    {
        private readonly IPlatformServiceProvider _previousProvider;

        public TimeoutHarness(Type fixtureType)
        {
            _previousProvider = PlatformServiceProvider.Instance;
            Services = new TestablePlatformServiceProvider();
            Services.SetReflectionOperations(new ReflectionOperations());
            PlatformServiceProvider.Instance = Services;

            Context = new TestContextImplementation(null, fixtureType.FullName, new Dictionary<string, object?>(), null, null);
            var assemblyInfo = new TestAssemblyInfo(typeof(TimeoutHarness).Assembly);
            var classInfo = new TestClassInfo(fixtureType, fixtureType.GetConstructor(Type.EmptyTypes), true, new TestClassAttribute(), assemblyInfo);
            Subject = new TestMethodInfo(fixtureType.GetMethod(TestMethodName)!, classInfo)
            {
                TestContext = Context,
                TimeoutInfo = TimeoutInfo.FromTimeout(60_000),
            };
        }

        public TestablePlatformServiceProvider Services { get; }

        public TestContextImplementation Context { get; }

        public TestMethodInfo Subject { get; }

        public void Dispose()
            => PlatformServiceProvider.Instance = _previousProvider;
    }

    private sealed class SelfCancelingFixture
    {
        public TestContext? TestContext { get; set; }

        public void TestMethod()
            => TestContext!.CancellationTokenSource.Cancel();
    }

    private sealed class RunCancelingFixture
    {
        public static ManualResetEventSlim? ReleaseTest { get; set; }

        public TestContext? TestContext { get; set; }

        public void TestMethod()
        {
            PlatformServiceProvider.Instance.TestRunCancellationToken!.Cancel();
            ReleaseTest!.Wait();
        }
    }
}
