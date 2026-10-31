// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Execution;

public sealed class LifecycleContinuationTests : TestContainer
{
    public async Task InitializationCacheDoesNotReplayFirstResultOutput()
    {
        using var harness = new LifecycleHarness();
        TestContextImplementation context = harness.CreateContext();
        TestResult first = await harness.Class.GetResultOrRunClassInitializeAsync(
            context, "assembly-output", "assembly-error", "assembly-trace", "assembly-messages");
        TestResult cached = await harness.Class.GetResultOrRunClassInitializeAsync(context, null, null, null, null);

        first.Outcome.Should().Be(UnitTestOutcome.Passed);
        cached.Outcome.Should().Be(UnitTestOutcome.Passed);
        cached.Should().NotBeSameAs(first);
        first.LogOutput.Should().Contain("assembly-output");
        first.LogError.Should().Contain("assembly-error");
        first.DebugTrace.Should().Contain("assembly-trace");
        first.TestContextMessages.Should().Contain("assembly-messages").And.Contain("class initialized");
        cached.LogOutput.Should().BeNull();
        cached.LogError.Should().BeNull();
        cached.DebugTrace.Should().BeNull();
        cached.TestContextMessages.Should().BeNull();
        harness.Events.Should().Equal("initialize-class");
        harness.Class.PostClassInitProperties!["class"].Should().Be("C");
    }

    public async Task ConcurrentInitializationWaiterUsesThePublishedResultExactlyOnce()
    {
        using var harness = new LifecycleHarness();
        var entered = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        TestContextImplementation firstContext = harness.CreateContext();
        firstContext.Properties["entered"] = entered;
        firstContext.Properties["release"] = release.Task;
        Task<TestResult> first = harness.Class.GetResultOrRunClassInitializeAsync(firstContext, null, null, null, null);
        await entered.Task;
        Task<TestResult> second = harness.Class.GetResultOrRunClassInitializeAsync(harness.CreateContext(), null, null, null, null);
        try
        {
            second.IsCompleted.Should().BeFalse();
        }
        finally
        {
            release.TrySetResult(null);
        }

        TestResult[] results = await Task.WhenAll(first, second);
        results.Select(result => result.Outcome).Should().Equal(UnitTestOutcome.Passed, UnitTestOutcome.Passed);
        results[1].Should().NotBeSameAs(results[0]);
        harness.Events.Should().Equal("initialize-class");
    }

    public async Task CanceledClassFixturesRecordFailuresWithoutInvokingUserCode()
    {
        using var harness = new LifecycleHarness();
        TestContextImplementation context = harness.CreateContext();
        context.CancellationTokenSource.Cancel();
        MethodInfo initialize = Method(nameof(LifecycleFixture.InitializeClass));
        MethodInfo cleanup = Method(nameof(LifecycleFixture.CleanupClass));
        harness.Class.ClassInitializeMethodTimeoutMilliseconds[initialize] = TimeoutInfo.FromTimeout(int.MaxValue);
        harness.Class.ClassCleanupMethodTimeoutMilliseconds[cleanup] = TimeoutInfo.FromTimeout(int.MaxValue);
        List<string> spans = [];
        List<Exception> failures = [];
        MSTestInstrumentation.SetActivityFactory((name, _) => new RecordedActivity(name, spans, failures));
        try
        {
            TestResult initialized = await harness.Class.GetResultOrRunClassInitializeAsync(context, null, null, null, null);
            TestResult? cleaned = await harness.Class.RunClassCleanupAsync(context, []);

            initialized.Outcome.Should().Be(UnitTestOutcome.Timeout);
            initialized.TestFailureException.Should().BeOfType<TestFailedException>();
            cleaned!.Outcome.Should().Be(UnitTestOutcome.Failed);
            ((TestFailedException)cleaned.TestFailureException!).Outcome.Should().Be(UnitTestOutcome.Timeout);
            harness.Events.Should().BeEmpty();
            failures.Should().HaveCount(2);
            spans.Should().Equal("MSTest.ClassInitialize", "MSTest.ClassCleanup");
        }
        finally
        {
            MSTestInstrumentation.SetActivityFactory(null);
        }
    }

    public async Task ContextAccessFailureBecomesAnErrorResult()
    {
        using var harness = new LifecycleHarness();
        var context = new Mock<ITestContext>();
        context.SetupGet(value => value.Context).Throws(new InvalidOperationException("context unavailable"));

        TestResult result = await harness.Class.GetResultOrRunClassInitializeAsync(context.Object, null, null, null, null);

        result.Outcome.Should().Be(UnitTestOutcome.Error);
        result.TestFailureException.Should().BeOfType<TestFailedException>().Which.Message.Should().Be("context unavailable");
        harness.Events.Should().BeEmpty();
    }

    public async Task CleanupWrapsOriginalFailureAndDoesNotRunTwice()
    {
        using var harness = new LifecycleHarness();
        harness.Class.IsClassInitializeExecuted = true;
        TestContextImplementation context = harness.CreateContext();
        var original = new TestFailedException(UnitTestOutcome.Timeout, "original cleanup failure");
        context.Properties["throw"] = original;

        TestResult? result = await harness.Class.RunClassCleanupAsync(context, []);
        TestResult? repeated = await harness.Class.RunClassCleanupAsync(context, []);

        result!.Outcome.Should().Be(UnitTestOutcome.Failed);
        result.DisplayName.Should().Be($"[{typeof(LifecycleFixture).FullName} ClassCleanup]");
        result.TestFailureException.Should().BeOfType<TestFailedException>().Which.InnerException.Should().BeSameAs(original);
        repeated.Should().BeNull();
        harness.Events.Should().HaveCount(1);
    }

    public async Task SuccessfulCleanupAppendsOutputOnlyToTheLastResult()
    {
        using var harness = new LifecycleHarness();
        harness.Class.IsClassInitializeExecuted = true;
        TestResult[] results = [new() { Outcome = UnitTestOutcome.Passed }, new() { Outcome = UnitTestOutcome.Passed }];

        (await harness.Class.RunClassCleanupAsync(harness.CreateContext(), results)).Should().BeNull();

        results[0].TestContextMessages.Should().BeNull();
        results[1].TestContextMessages.Should().Contain("class cleaned");
        harness.Class.IsClassCleanupExecuted.Should().BeTrue();
    }

    public async Task ForceCleanupFlowsClassAndAssemblyPropertiesWithoutLeakingClassScope()
    {
        using var harness = new LifecycleHarness(useCache: true);
        await harness.InitializeAsync();
        harness.Events.Clear();

        ClassCleanupManager.ForceCleanup(harness.Cache!, harness.SourceParameters, Mock.Of<IAdapterMessageLogger>());

        harness.Events.Should().Equal("class:A:C:class:source", "assembly:A:-:assembly:source");
    }

    public async Task ForceCleanupPropagatesClassAndAssemblyFailures()
    {
        foreach (string phase in new[] { "class", "assembly" })
        {
            using var harness = new LifecycleHarness(useCache: true);
            await harness.InitializeAsync();
            harness.Events.Clear();
            harness.SourceParameters["phase"] = phase;
            Action cleanup = () => ClassCleanupManager.ForceCleanup(harness.Cache!, harness.SourceParameters, Mock.Of<IAdapterMessageLogger>());

            TestFailedException failure = cleanup.Should().ThrowExactly<TestFailedException>().Which;
            failure.Outcome.Should().Be(UnitTestOutcome.Failed);
            failure.InnerException.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Be(phase + " failure");
            harness.Events.Should().HaveCount(phase == "class" ? 1 : 2);
        }
    }

    public async Task EmptyInitializationStillPermitsCleanup()
    {
        using var harness = new LifecycleHarness();
        var empty = new TestClassInfo(typeof(LifecycleFixture), typeof(LifecycleFixture).GetConstructor(Type.EmptyTypes), true,
            new TestClassAttribute(), harness.Assembly)
        {
            ClassCleanupMethod = Method(nameof(LifecycleFixture.CleanupClass)),
        };
        TestContextImplementation context = harness.CreateContext();

        await empty.RunClassInitializeAsync(context);
        (await empty.ExecuteClassCleanupAsync(context)).Should().BeNull();

        harness.Events.Should().ContainSingle();
        empty.IsClassInitializeExecuted.Should().BeTrue();
        empty.IsClassCleanupExecuted.Should().BeTrue();
    }

    public async Task StaJoinInterruptionUsesTheLifecycleErrorContract()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            using var harness = new LifecycleHarness();
            var services = (TestablePlatformServiceProvider)PlatformServiceProvider.Instance;
            services.MockTraceLogger.SetupGet(value => value.IsWarningEnabled).Returns(true);
            List<string> warnings = [];
            services.MockTraceLogger.Setup(value => value.Warning(It.IsAny<string>())).Callback<string>(warnings.Add);
            var info = new TestClassInfo(typeof(LifecycleFixture), typeof(LifecycleFixture).GetConstructor(Type.EmptyTypes), true,
                new STATestClassAttribute(), harness.Assembly)
            {
                ClassInitializeMethod = Method(nameof(LifecycleFixture.InitializeClass)),
                ClassCleanupMethod = Method(nameof(LifecycleFixture.CleanupClass)),
            };
            TestContextImplementation context = harness.CreateContext();

            (await info.GetResultOrRunClassInitializeAsync(context, null, null, null, null)).Outcome.Should().Be(UnitTestOutcome.Passed);
            (await info.RunClassCleanupAsync(context, [])).Should().BeNull();
            warnings.Should().Equal(
                Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources.Resource.STAIsOnlySupportedOnWindowsWarning,
                Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources.Resource.STAIsOnlySupportedOnWindowsWarning);
            harness.Events.Should().HaveCount(2);
            return;
        }

        foreach (bool initialize in new[] { true, false })
        {
            using var harness = new LifecycleHarness();
            var services = (TestablePlatformServiceProvider)PlatformServiceProvider.Instance;
            services.MockTraceLogger.SetupGet(value => value.IsErrorEnabled).Returns(true);
            List<string> errors = [];
            services.MockTraceLogger.Setup(value => value.Error(It.IsAny<string>())).Callback<string>(errors.Add);
            var info = new TestClassInfo(typeof(LifecycleFixture), typeof(LifecycleFixture).GetConstructor(Type.EmptyTypes), true,
                new STATestClassAttribute(), harness.Assembly)
            {
                ClassInitializeMethod = Method(nameof(LifecycleFixture.InterruptInitializer)),
                ClassCleanupMethod = Method(nameof(LifecycleFixture.InterruptCleanup)),
                IsClassInitializeExecuted = !initialize,
            };
            var release = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var entered = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var outcome = new TaskCompletionSource<TestResult?>(TaskCreationOptions.RunContinuationsAsynchronously);
            TestContextImplementation context = harness.CreateContext();
            context.Properties["release"] = release.Task;
            context.Properties["entry-started"] = entered;
            var caller = new Thread(() =>
            {
                try
                {
                    TestResult? result = initialize
                        ? info.GetResultOrRunClassInitializeAsync(context, null, null, null, null).GetAwaiter().GetResult()
                        : info.RunClassCleanupAsync(context, []).GetAwaiter().GetResult();
                    outcome.TrySetResult(result);
                }
                catch (Exception exception)
                {
                    outcome.TrySetException(exception);
                }
            })
            {
                Name = "lifecycle-owned-caller",
                IsBackground = true,
            };
            caller.SetApartmentState(ApartmentState.MTA);
            caller.Start();
            try
            {
                Task first = await Task.WhenAny(entered.Task, outcome.Task).ConfigureAwait(false);
                first.Should().BeSameAs(entered.Task);
                SpinWait.SpinUntil(() => outcome.Task.IsCompleted || (caller.ThreadState & System.Threading.ThreadState.WaitSleepJoin) != 0);
                outcome.Task.IsCompleted.Should().BeFalse();
                caller.Interrupt();
                TestResult? result = await outcome.Task.ConfigureAwait(false);
                if (initialize)
                {
                    result!.Outcome.Should().Be(UnitTestOutcome.Error);
                    result.TestFailureException.Should().BeOfType<TestFailedException>()
                        .Which.Message.Should().Be(new ThreadInterruptedException().Message);
                }
                else
                {
                    result.Should().BeNull();
                }

                errors.Should().ContainSingle();
                errors[0].Should().StartWith("Failed to join STA thread 'MSTest STATestClass Class")
                    .And.Contain(nameof(ThreadInterruptedException));
            }
            finally
            {
                release.TrySetResult(null);
                caller.Join();
                if (context.Properties.TryGetValue("entry-thread", out object? entry))
                {
                    ((Thread)entry!).Join();
                }
            }
        }
    }

    private static MethodInfo Method(string name) => typeof(LifecycleFixture).GetMethod(name)!;

    private sealed class LifecycleHarness : IDisposable
    {
        private readonly IPlatformServiceProvider _previous;

        internal LifecycleHarness(bool useCache = false)
        {
            _previous = PlatformServiceProvider.Instance;
            var services = new TestablePlatformServiceProvider();
            services.SetReflectionOperations(new ReflectionOperations());
            services.MockFileOperations.Setup(value => value.LoadAssembly("lifecycle-source")).Returns(typeof(LifecycleFixture).Assembly);
            PlatformServiceProvider.Instance = services;
            SourceParameters = new Dictionary<string, object?> { ["events"] = Events, ["source"] = "source" };
            if (useCache)
            {
                var reflection = new Mock<ReflectHelper>();
                reflection.Setup(value => value.IsAttributeDefined<TestClassAttribute>(typeof(LifecycleFixture))).Returns(true);
                reflection.Setup(value => value.IsAttributeDefined<ClassInitializeAttribute>(Method(nameof(LifecycleFixture.InitializeClass)))).Returns(true);
                reflection.Setup(value => value.IsAttributeDefined<ClassCleanupAttribute>(Method(nameof(LifecycleFixture.CleanupClass)))).Returns(true);
                reflection.Setup(value => value.IsAttributeDefined<AssemblyInitializeAttribute>(Method(nameof(LifecycleFixture.InitializeAssembly)))).Returns(true);
                reflection.Setup(value => value.IsAttributeDefined<AssemblyCleanupAttribute>(Method(nameof(LifecycleFixture.CleanupAssembly)))).Returns(true);
                Cache = new TypeCache(reflection.Object);
                _ = Cache.GetTestMethodInfo(new TestMethod(
                    nameof(LifecycleFixture.Test), null, nameof(LifecycleFixture.Test), typeof(LifecycleFixture).FullName!, "lifecycle-source", null, null));
                Class = Cache.ClassInfoCache.Single()!;
                Assembly = Cache.AssemblyInfoCache.Single();
            }
            else
            {
                Assembly = new TestAssemblyInfo(typeof(LifecycleFixture).Assembly);
                Class = new TestClassInfo(typeof(LifecycleFixture), typeof(LifecycleFixture).GetConstructor(Type.EmptyTypes), true, new TestClassAttribute(), Assembly)
                {
                    ClassInitializeMethod = Method(nameof(LifecycleFixture.InitializeClass)),
                    ClassCleanupMethod = Method(nameof(LifecycleFixture.CleanupClass)),
                };
            }
        }

        internal List<string> Events { get; } = [];

        internal Dictionary<string, object?> SourceParameters { get; }

        internal TypeCache? Cache { get; }

        internal TestClassInfo Class { get; }

        internal TestAssemblyInfo Assembly { get; }

        internal TestContextImplementation CreateContext()
            => new(null, typeof(LifecycleFixture).FullName, new Dictionary<string, object?> { ["events"] = Events }, Mock.Of<IAdapterMessageLogger>(), null);

        internal async Task InitializeAsync()
        {
            TestContextImplementation assembly = CreateContext();
            (await Assembly.RunAssemblyInitializeAsync(assembly)).Outcome.Should().Be(UnitTestOutcome.Passed);
            TestContextImplementation context = CreateContext();
            context.MergeProperties(Assembly.PostAssemblyInitProperties);
            (await Class.GetResultOrRunClassInitializeAsync(context, null, null, null, null)).Outcome.Should().Be(UnitTestOutcome.Passed);
        }

        public void Dispose()
        {
            MSTestInstrumentation.SetActivityFactory(null);
            MSTestSettings.Reset();
            PlatformServiceProvider.Instance = _previous;
        }
    }

    private sealed class RecordedActivity(string name, List<string> spans, List<Exception> failures) : IMSTestActivity
    {
        public void SetTag(string key, object? value)
        {
        }

        public void RecordException(Exception exception) => failures.Add(exception);

        public void SetFailed(string? description)
        {
        }

        public void Dispose() => spans.Add(name);
    }

    [TestClass]
    public sealed class LifecycleFixture
    {
        public void Test()
        {
        }

        public static void InitializeAssembly(TestContext context)
        {
            context.Properties["assembly"] = "A";
            context.Properties["shared"] = "assembly";
        }

        public static async Task InitializeClass(TestContext context)
        {
            ((List<string>)context.Properties["events"]!).Add("initialize-class");
            context.Properties["class"] = "C";
            context.Properties["shared"] = "class";
            context.WriteLine("class initialized");
            if (context.Properties.TryGetValue("entered", out object? entered))
            {
                ((TaskCompletionSource<object?>)entered!).TrySetResult(null);
                await ((Task)context.Properties["release"]!).ConfigureAwait(false);
            }
        }

        public static void CleanupClass(TestContext context)
        {
            var events = (List<string>)context.Properties["events"]!;
            context.Properties.TryGetValue("assembly", out object? assembly);
            context.Properties.TryGetValue("class", out object? classValue);
            context.Properties.TryGetValue("shared", out object? shared);
            context.Properties.TryGetValue("source", out object? source);
            events.Add($"class:{assembly ?? "-"}:{classValue ?? "-"}:{shared ?? "-"}:{source ?? "-"}");
            context.WriteLine("class cleaned");
            if (context.Properties.TryGetValue("throw", out object? failure))
            {
                throw (Exception)failure!;
            }

            if (context.Properties.TryGetValue("phase", out object? phase) && Equals(phase, "class"))
            {
                throw new InvalidOperationException("class failure");
            }
        }

        public static void CleanupAssembly(TestContext context)
        {
            var events = (List<string>)context.Properties["events"]!;
            context.Properties.TryGetValue("class", out object? classValue);
            events.Add($"assembly:{context.Properties["assembly"]}:{classValue ?? "-"}:{context.Properties["shared"]}:{context.Properties["source"]}");
            if (context.Properties.TryGetValue("phase", out object? phase) && Equals(phase, "assembly"))
            {
                throw new InvalidOperationException("assembly failure");
            }
        }

        public static void InterruptInitializer(TestContext context) => InterruptOwnedCaller(context);

        public static void InterruptCleanup(TestContext context) => InterruptOwnedCaller(context);

        private static void InterruptOwnedCaller(TestContext context)
        {
            context.Properties["entry-thread"] = Thread.CurrentThread;
            ((TaskCompletionSource<object?>)context.Properties["entry-started"]!).TrySetResult(null);
            ((Task)context.Properties["release"]!).GetAwaiter().GetResult();
        }
    }
}
