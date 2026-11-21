// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.Testing.TestInfrastructure;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Execution;

public sealed class TestClassInfoInitializerMutationTests : TestContainer
{
    private const string CachedResultMessage = "Class initialize result should be available if and only if class initialize was executed";
    private const string ExecutedMessage = "Caller shouldn't call us if it was executed.";

    public async Task ClassInitializeWithNothingToInitialize_ReportsTheContractViolationOnce_AndMarksTheClassAsExecuted()
    {
        using var harness = new InitializerHarness();
#if DEBUG && !NETFRAMEWORK
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);

        await harness.Class.RunClassInitializeAsync(harness.CreateContext());

        assertions.Should().Equal("Caller shouldn't call us if nothing to execute");
        harness.Class.IsClassInitializeExecuted.Should().BeTrue();
        harness.Class.ClassInitializationException.Should().BeNull();
#else
        await Task.CompletedTask;
#endif
    }

    public async Task ClassInitializeThatWasAlreadyExecuted_ReportsTheContractViolation_AndReportsTheStoredFailureWithoutAMethodName()
    {
        using var harness = new InitializerHarness();
        harness.Class.ClassInitializeMethod = Method(nameof(InitializerFixture.InitializeClass));
        harness.Class.IsClassInitializeExecuted = true;
        harness.Class.ClassInitializationException = Thrown("stored failure");
#if DEBUG && !NETFRAMEWORK
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);

        Func<Task> run = () => harness.Class.RunClassInitializeAsync(harness.CreateContext());
        TestFailedException exception = (await run.Should().ThrowExactlyAsync<TestFailedException>()).Which;

        assertions.Should().Equal(ExecutedMessage);
        exception.Outcome.Should().Be(UnitTestOutcome.Failed);
        exception.Message.Should().Be(string.Format(
            CultureInfo.CurrentCulture,
            Resource.UTA_ClassInitMethodThrows,
            typeof(InitializerFixture).FullName,
            string.Empty,
            typeof(InvalidOperationException).ToString(),
            "stored failure"));
        harness.Class.ClassInitializationException.Should().BeSameAs(exception);
        harness.Events.Should().BeEmpty();
#else
        await Task.CompletedTask;
#endif
    }

    public async Task BaseClassInitializeMethods_RunFromTheLeastDerivedToTheMostDerived_BeforeTheClassInitializeMethod()
    {
        using var harness = new InitializerHarness();
        harness.Class.BaseClassInitMethods.Add(Method(nameof(InitializerFixture.DerivedBase)));
        harness.Class.BaseClassInitMethods.Add(Method(nameof(InitializerFixture.RootBase)));
        harness.Class.ClassInitializeMethod = Method(nameof(InitializerFixture.InitializeClass));

        await harness.Class.RunClassInitializeAsync(harness.CreateContext());

        harness.Events.Should().Equal("root-base", "derived-base", "class");
        harness.Class.ClassInitializationException.Should().BeNull();
        harness.Class.IsClassInitializeExecuted.Should().BeTrue();
    }

    public async Task FailingBaseClassInitializeMethod_PreventsTheRemainingInitializersFromRunning_AndIsReported()
    {
        using var harness = new InitializerHarness();
        MethodInfo root = Method(nameof(InitializerFixture.RootBase));
        harness.Class.BaseClassInitMethods.Add(Method(nameof(InitializerFixture.DerivedBase)));
        harness.Class.BaseClassInitMethods.Add(root);
        harness.Class.ClassInitializeMethod = Method(nameof(InitializerFixture.InitializeClass));
        harness.Class.ClassInitializeMethodTimeoutMilliseconds[root] = ZeroCooperativeTimeout();

        Func<Task> run = () => harness.Class.RunClassInitializeAsync(harness.CreateContext());
        TestFailedException exception = (await run.Should().ThrowExactlyAsync<TestFailedException>()).Which;

        exception.Outcome.Should().Be(UnitTestOutcome.Timeout);
        exception.Message.Should().Be(string.Format(
            CultureInfo.InvariantCulture,
            Resource.ClassInitializeTimedOut,
            typeof(InitializerFixture).FullName,
            nameof(InitializerFixture.RootBase),
            0));
        harness.Events.Should().BeEmpty();
        harness.Class.ClassInitializationException.Should().BeSameAs(exception);
        harness.Class.IsClassInitializeExecuted.Should().BeTrue();
    }

    public async Task ThrowingBaseClassInitializeMethod_ReportsTheNameOfTheMethodThatThrew()
    {
        using var harness = new InitializerHarness();
        harness.Class.BaseClassInitMethods.Add(Method(nameof(InitializerFixture.ThrowingBase)));
        harness.Class.ClassInitializeMethod = Method(nameof(InitializerFixture.InitializeClass));

        Func<Task> run = () => harness.Class.RunClassInitializeAsync(harness.CreateContext());
        TestFailedException exception = (await run.Should().ThrowExactlyAsync<TestFailedException>()).Which;

        exception.Outcome.Should().Be(UnitTestOutcome.Failed);
        exception.Message.Should().Be(string.Format(
            CultureInfo.CurrentCulture,
            Resource.UTA_ClassInitMethodThrows,
            typeof(InitializerFixture).FullName,
            nameof(InitializerFixture.ThrowingBase),
            typeof(InvalidOperationException).ToString(),
            "base failure"));
        harness.Events.Should().Equal("throwing-base");
    }

    public async Task BaseClassInitializeMethodsWithoutAClassInitializeMethod_CompleteWithoutFailure()
    {
        using var harness = new InitializerHarness();
        harness.Class.BaseClassInitMethods.Add(Method(nameof(InitializerFixture.RootBase)));

        await harness.Class.RunClassInitializeAsync(harness.CreateContext());

        harness.Events.Should().Equal("root-base");
        harness.Class.ClassInitializationException.Should().BeNull();
        harness.Class.IsClassInitializeExecuted.Should().BeTrue();
    }

    public async Task CachedResultOfAClassWhoseInitializationNeverRan_ReportsTheContractViolation_OnTheFastPath()
    {
        using var harness = new InitializerHarness();
        harness.Class.ClassInitializeMethod = Method(nameof(InitializerFixture.InitializeClass));
        PublishResultWithoutInitializing(harness.Class);
#if DEBUG && !NETFRAMEWORK
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);

        TestResult result = await harness.Class.GetResultOrRunClassInitializeAsync(harness.CreateContext(), null, null, null, null);

        result.Outcome.Should().Be(UnitTestOutcome.Passed);
        assertions.Should().Equal(CachedResultMessage);
        harness.Events.Should().BeEmpty();
#else
        await Task.CompletedTask;
#endif
    }

    public async Task ClassWithoutInitializeMethods_PublishesAPassedResultForLaterCallers()
    {
        using var harness = new InitializerHarness();

        TestResult first = await harness.Class.GetResultOrRunClassInitializeAsync(harness.CreateContext(), null, null, null, null);

        first.Outcome.Should().Be(UnitTestOutcome.Passed);
        harness.Class.IsClassInitializeExecuted.Should().BeTrue();
        TestResult? cached = harness.Class.TryGetClonedCachedClassInitializeResult();
        cached.Should().NotBeNull();
        cached!.Outcome.Should().Be(UnitTestOutcome.Passed);
    }

    public async Task ResultPublishedWhileWaitingForTheLock_ReportsTheContractViolation_OnTheLockedPath()
    {
        using var harness = new InitializerHarness();
        harness.Class.ClassInitializeMethod = Method(nameof(InitializerFixture.InitializeClass));
        var semaphore = (SemaphoreSlim)typeof(TestClassInfo).GetField("_testClassExecuteSyncSemaphore", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(harness.Class)!;
#if DEBUG && !NETFRAMEWORK
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);

        await semaphore.WaitAsync();
        Task<TestResult> pending = harness.Class.GetResultOrRunClassInitializeAsync(harness.CreateContext(), null, null, null, null);
        pending.IsCompleted.Should().BeFalse();
        PublishResultWithoutInitializing(harness.Class);
        semaphore.Release();

        TestResult result = await pending;

        result.Outcome.Should().Be(UnitTestOutcome.Passed);
        assertions.Should().Equal(CachedResultMessage);
        harness.Events.Should().BeEmpty();
#else
        semaphore.Should().NotBeNull();
        await Task.CompletedTask;
#endif
    }

    public async Task InitializationMarkedAsExecutedWithoutAResult_ReportsTheContractViolations_AndStillCompletes()
    {
        using var harness = new InitializerHarness();
        harness.Class.ClassInitializeMethod = Method(nameof(InitializerFixture.InitializeClass));
        harness.Class.IsClassInitializeExecuted = true;
#if DEBUG && !NETFRAMEWORK
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);

        TestResult result = await harness.Class.GetResultOrRunClassInitializeAsync(harness.CreateContext(), null, null, null, null);

        result.Outcome.Should().Be(UnitTestOutcome.Passed);
        assertions.Should().Equal(
            "If class initialize was executed, we should have been in the previous if were we have a result available.",
            ExecutedMessage);
        harness.Events.Should().BeEmpty();
#else
        await Task.CompletedTask;
#endif
    }

    public async Task ClassInitializeActivity_IsStartedWithTheFixtureKindTheOwningTypeAndTheAssembly()
    {
        using var harness = new InitializerHarness();
        harness.Class.ClassInitializeMethod = Method(nameof(InitializerFixture.InitializeClass));
        var started = new List<string>();
        MSTestInstrumentation.SetActivityFactory((name, tags) =>
        {
            started.Add($"{name}|{string.Join(",", tags!.Select(tag => $"{tag.Key}={tag.Value}"))}");
            return new ActivityProbe();
        });

        await harness.Class.RunClassInitializeAsync(harness.CreateContext());

        started.Should().Equal(
            $"{MSTestInstrumentation.ActivityNames.ClassInitialize}|"
            + $"{MSTestInstrumentation.Attributes.FixtureKind}=class_initialize,"
            + $"{MSTestInstrumentation.Attributes.TestClass}={typeof(InitializerFixture).FullName},"
            + $"{MSTestInstrumentation.Attributes.TestAssembly}={typeof(InitializerFixture).Assembly.GetName().Name}");
    }

    public async Task ClassInitialize_RunsOnTheExecutionContextOfTheClass_WhenTheAssemblyHasItsOwn()
    {
        using var harness = new InitializerHarness();
        harness.Class.ClassInitializeMethod = Method(nameof(InitializerFixture.InitializeClassObservingTheContext));
        harness.Class.ExecutionContext = CaptureWith("class");
        harness.Assembly.ExecutionContext = CaptureWith("assembly");

        await harness.Class.RunClassInitializeAsync(harness.CreateContext());

        harness.Events.Should().Equal("class");
    }

    public async Task ClassInitialize_FallsBackToTheExecutionContextOfTheAssembly_WhenTheClassHasNone()
    {
        using var harness = new InitializerHarness();
        harness.Class.ClassInitializeMethod = Method(nameof(InitializerFixture.InitializeClassObservingTheContext));
        harness.Assembly.ExecutionContext = CaptureWith("assembly");
        InitializerFixture.Marker.Value = "caller";
        try
        {
            await harness.Class.RunClassInitializeAsync(harness.CreateContext());
        }
        finally
        {
            InitializerFixture.Marker.Value = null;
        }

        harness.Events.Should().Equal("assembly");
    }

    public void TestContextPropertyThatIsNotPublic_IsNotResolved()
    {
        using var harness = new InitializerHarness();

        var info = new TestClassInfo(
            typeof(ClassWithInternalTestContext),
            typeof(ClassWithInternalTestContext).GetConstructor(Type.EmptyTypes),
            true,
            new TestClassAttribute(),
            harness.Assembly);

        info.TestContextProperty.Should().BeNull();
    }

    public void SettingTheTestInitializeMethodTwice_ThrowsTheMultipleInitializeError()
    {
        using var harness = new InitializerHarness();
        harness.Class.TestInitializeMethod = Method(nameof(InitializerFixture.InitializeClass));

        Action second = () => harness.Class.TestInitializeMethod = Method(nameof(InitializerFixture.RootBase));

        second.Should().Throw<TypeInspectionException>().Which.Message.Should().Be(string.Format(
            CultureInfo.CurrentCulture,
            Resource.UTA_ErrorMultiInit,
            typeof(InitializerFixture).FullName));
    }

    private static MethodInfo Method(string name) => typeof(InitializerFixture).GetMethod(name)!;

    private static TimeoutInfo ZeroCooperativeTimeout()
        => TimeoutInfo.FromTimeoutAttribute(new TimeoutAttribute(0) { CooperativeCancellation = true });

    // The stack trace of an exception that was never thrown is null, which StackTraceInformation asserts on.
    private static InvalidOperationException Thrown(string message)
    {
        try
        {
            throw new InvalidOperationException(message);
        }
        catch (InvalidOperationException ex)
        {
            return ex;
        }
    }

    private static void PublishResultWithoutInitializing(TestClassInfo info)
        => typeof(TestClassInfo).GetField("_classInitializeResult", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(info, new TestResult { Outcome = UnitTestOutcome.Passed });

    private static ExecutionContext CaptureWith(string value)
    {
        InitializerFixture.Marker.Value = value;
        try
        {
            return ExecutionContext.Capture()!;
        }
        finally
        {
            InitializerFixture.Marker.Value = null;
        }
    }

    private sealed class InitializerHarness : IDisposable
    {
        private readonly IPlatformServiceProvider _previous;

        internal InitializerHarness()
        {
            _previous = PlatformServiceProvider.Instance;
            var services = new TestablePlatformServiceProvider();
            services.SetReflectionOperations(new ReflectionOperations());
            PlatformServiceProvider.Instance = services;
            Assembly = new TestAssemblyInfo(typeof(InitializerFixture).Assembly);
            Class = new TestClassInfo(typeof(InitializerFixture), typeof(InitializerFixture).GetConstructor(Type.EmptyTypes), true, new TestClassAttribute(), Assembly);
        }

        internal List<string> Events { get; } = [];

        internal TestClassInfo Class { get; }

        internal TestAssemblyInfo Assembly { get; }

        internal TestContextImplementation CreateContext()
            => new(null, typeof(InitializerFixture).FullName, new Dictionary<string, object?> { ["events"] = Events }, Mock.Of<IAdapterMessageLogger>(), null);

        public void Dispose()
        {
            MSTestInstrumentation.SetActivityFactory(null);
            MSTestSettings.Reset();
            PlatformServiceProvider.Instance = _previous;
        }
    }

    private sealed class ActivityProbe : IMSTestActivity
    {
        public void SetTag(string key, object? value)
        {
        }

        public void RecordException(Exception exception)
        {
        }

        public void SetFailed(string? description)
        {
        }

        public void Dispose()
        {
        }
    }

    public sealed class InitializerFixture
    {
        internal static readonly AsyncLocal<string?> Marker = new();

        public static void RootBase(TestContext context) => Record(context, "root-base");

        public static void DerivedBase(TestContext context) => Record(context, "derived-base");

        public static void InitializeClass(TestContext context) => Record(context, "class");

        public static void InitializeClassObservingTheContext(TestContext context) => Record(context, Marker.Value ?? "none");

        public static void ThrowingBase(TestContext context)
        {
            Record(context, "throwing-base");
            throw new InvalidOperationException("base failure");
        }

        private static void Record(TestContext context, string entry) => ((List<string>)context.Properties["events"]!).Add(entry);
    }

    public sealed class ClassWithInternalTestContext
    {
        internal TestContext? TestContext { get; set; }
    }
}
