// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

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

public sealed class TestClassInfoCleanupMutationTests : TestContainer
{
    public async Task ClassCleanupWithoutAnyCleanupMethod_ReturnsWithoutWaitingForTheClassLock_AndLeavesTheClassUncleaned()
    {
        using var harness = new CleanupHarness();
        harness.Class.IsClassInitializeExecuted = true;
        SemaphoreSlim classLock = LockOf(harness.Class);

        await classLock.WaitAsync();
        try
        {
            Task<TestFailedException?> cleanup = harness.Class.ExecuteClassCleanupAsync(harness.CreateContext());

            cleanup.IsCompleted.Should().BeTrue("a class without cleanup methods must not contend for the class lock");
            (await cleanup).Should().BeNull();
        }
        finally
        {
            classLock.Release();
        }

        harness.Class.IsClassCleanupExecuted.Should().BeFalse();
    }

    public async Task ClassCleanupThatAlreadyRan_ReturnsWithoutWaitingForTheClassLock_AndDoesNotRunTheCleanupMethodsAgain()
    {
        using var harness = new CleanupHarness();
        harness.Class.ClassCleanupMethod = Method(nameof(CleanupFixture.CleanupClass));
        harness.Class.BaseClassCleanupMethods.Add(Method(nameof(CleanupFixture.FirstBaseCleanup)));
        harness.Class.IsClassInitializeExecuted = true;
        harness.Class.IsClassCleanupExecuted = true;
        SemaphoreSlim classLock = LockOf(harness.Class);

        await classLock.WaitAsync();
        try
        {
            Task<TestFailedException?> cleanup = harness.Class.ExecuteClassCleanupAsync(harness.CreateContext());

            cleanup.IsCompleted.Should().BeTrue("a class whose cleanup already ran must not contend for the class lock");
            (await cleanup).Should().BeNull();
        }
        finally
        {
            classLock.Release();
        }

        harness.Events.Should().BeEmpty();
    }

    public async Task BaseClassCleanupMethods_RunInTheirRegistrationOrder_AfterTheClassCleanupMethod()
    {
        using var harness = new CleanupHarness();
        harness.Class.ClassCleanupMethod = Method(nameof(CleanupFixture.CleanupClass));
        harness.Class.BaseClassCleanupMethods.Add(Method(nameof(CleanupFixture.FirstBaseCleanup)));
        harness.Class.BaseClassCleanupMethods.Add(Method(nameof(CleanupFixture.SecondBaseCleanup)));
        harness.Class.IsClassInitializeExecuted = true;

        TestFailedException? failure = await harness.Class.ExecuteClassCleanupAsync(harness.CreateContext());

        failure.Should().BeNull();
        harness.Events.Should().Equal("class", "first-base", "second-base");
        harness.Class.ClassCleanupException.Should().BeNull();
        harness.Class.IsClassCleanupExecuted.Should().BeTrue();
    }

    public async Task FailingBaseClassCleanupMethod_PreventsTheRemainingBaseCleanupMethodsFromRunning_AndIsReported()
    {
        using var harness = new CleanupHarness();
        MethodInfo first = Method(nameof(CleanupFixture.FirstBaseCleanup));
        harness.Class.BaseClassCleanupMethods.Add(first);
        harness.Class.BaseClassCleanupMethods.Add(Method(nameof(CleanupFixture.SecondBaseCleanup)));
        harness.Class.ClassCleanupMethodTimeoutMilliseconds[first] = ZeroCooperativeTimeout();
        harness.Class.IsClassInitializeExecuted = true;

        TestFailedException? failure = await harness.Class.ExecuteClassCleanupAsync(harness.CreateContext());

        failure.Should().NotBeNull();
        failure!.Outcome.Should().Be(UnitTestOutcome.Timeout);
        failure.Message.Should().Be(string.Format(
            CultureInfo.InvariantCulture,
            Resource.ClassCleanupTimedOut,
            typeof(CleanupFixture).FullName,
            nameof(CleanupFixture.FirstBaseCleanup),
            0));
        harness.Events.Should().BeEmpty();
        harness.Class.ClassCleanupException.Should().BeSameAs(failure);
        harness.Class.IsClassCleanupExecuted.Should().BeTrue();
    }

    public async Task RunClassCleanup_WithoutAnyCleanupMethod_ReturnsWithoutTouchingTheResults()
    {
        using var harness = new CleanupHarness();
        harness.Class.IsClassInitializeExecuted = true;
        TestContextImplementation context = harness.CreateContext();
        context.WriteLine("pending");
        TestResult[] results = [new TestResult { Outcome = UnitTestOutcome.Passed }];

        TestResult? cleanupResult = await harness.Class.RunClassCleanupAsync(context, results);

        cleanupResult.Should().BeNull();
        results[0].TestContextMessages.Should().BeNull();
    }

    public async Task RunClassCleanup_OfAClassWhoseCleanupAlreadyRan_ReturnsWithoutTouchingTheResults()
    {
        using var harness = new CleanupHarness();
        harness.Class.ClassCleanupMethod = Method(nameof(CleanupFixture.CleanupClass));
        harness.Class.IsClassInitializeExecuted = true;
        harness.Class.IsClassCleanupExecuted = true;
        TestContextImplementation context = harness.CreateContext();
        context.WriteLine("pending");
        TestResult[] results = [new TestResult { Outcome = UnitTestOutcome.Passed }];

        TestResult? cleanupResult = await harness.Class.RunClassCleanupAsync(context, results);

        cleanupResult.Should().BeNull();
        results[0].TestContextMessages.Should().BeNull();
        harness.Events.Should().BeEmpty();
    }

    public async Task RunClassCleanup_OfAClassThatIsNotAnStaTestClass_RunsOnTheCallingThread_WithoutRaisingTheNonWindowsWarning()
    {
        using var harness = new CleanupHarness();
        harness.Class.ClassCleanupMethod = Method(nameof(CleanupFixture.CleanupClassObservingTheThread));
        harness.Class.IsClassInitializeExecuted = true;
        harness.Services.MockTraceLogger.SetupGet(logger => logger.IsWarningEnabled).Returns(true);
        TestContextImplementation context = harness.CreateContext();

        TestResult? cleanupResult = await Task.Run(() => harness.Class.RunClassCleanupAsync(context, []));

        cleanupResult.Should().BeNull();
        harness.Events.Should().Equal("pool=True,sta=False");
        harness.Services.MockTraceLogger.Verify(logger => logger.Warning(Resource.STAIsOnlySupportedOnWindowsWarning), Times.Never);
    }

    public async Task RunClassCleanup_WhenTheCleanupFails_ReportsTheOutputOfTheTestContextOnTheFailedResult()
    {
        using var harness = new CleanupHarness();
        MethodInfo cleanup = Method(nameof(CleanupFixture.CleanupClass));
        harness.Class.ClassCleanupMethod = cleanup;
        harness.Class.ClassCleanupMethodTimeoutMilliseconds[cleanup] = ZeroCooperativeTimeout();
        harness.Class.IsClassInitializeExecuted = true;
        TestContextImplementation context = harness.CreateContext();
        context.WriteLine("pending");

        TestResult? failedResult = await harness.Class.RunClassCleanupAsync(context, []);

        failedResult.Should().NotBeNull();
        failedResult!.Outcome.Should().Be(UnitTestOutcome.Failed);
        failedResult.DisplayName.Should().Be($"[{typeof(CleanupFixture).FullName} ClassCleanup]");
        failedResult.TestFailureException.Should().BeSameAs(harness.Class.ClassCleanupException);
        failedResult.TestContextMessages.Should().NotBeNull();
        failedResult.TestContextMessages!.TrimEnd().Should().Be("pending");
        harness.Events.Should().BeEmpty();
    }

    public async Task ClassCleanupActivity_IsStartedWithTheFixtureKindTheOwningTypeAndTheAssembly()
    {
        using var harness = new CleanupHarness();
        harness.Class.ClassCleanupMethod = Method(nameof(CleanupFixture.CleanupClass));
        harness.Class.IsClassInitializeExecuted = true;
        var started = new List<string>();
        MSTestInstrumentation.SetActivityFactory((name, tags) =>
        {
            started.Add($"{name}|{string.Join(",", tags!.Select(tag => $"{tag.Key}={tag.Value}"))}");
            return new ActivityProbe();
        });

        TestFailedException? failure = await harness.Class.ExecuteClassCleanupAsync(harness.CreateContext());

        failure.Should().BeNull();
        started.Should().Equal(
            $"{MSTestInstrumentation.ActivityNames.ClassCleanup}|"
            + $"{MSTestInstrumentation.Attributes.FixtureKind}=class_cleanup,"
            + $"{MSTestInstrumentation.Attributes.TestClass}={typeof(CleanupFixture).FullName},"
            + $"{MSTestInstrumentation.Attributes.TestAssembly}={typeof(CleanupFixture).Assembly.GetName().Name}");
    }

    public async Task ClassCleanup_DoesNotInspectTheDeclaringTypeForTheActivity_WhenTracingIsDisabled()
    {
        int readsWithoutTracing = await CountDeclaringTypeReadsAsync(tracing: false);
        int readsWithTracing = await CountDeclaringTypeReadsAsync(tracing: true);

        readsWithoutTracing.Should().BeLessThan(readsWithTracing);
    }

    public async Task ClassCleanup_RunsOnTheExecutionContextOfTheClass_WhenTheAssemblyHasItsOwn()
    {
        using var harness = new CleanupHarness();
        harness.Class.ClassCleanupMethod = Method(nameof(CleanupFixture.CleanupClassObservingTheContext));
        harness.Class.IsClassInitializeExecuted = true;
        harness.Class.ExecutionContext = CaptureWith("class");
        harness.Assembly.ExecutionContext = CaptureWith("assembly");

        TestFailedException? failure = await harness.Class.ExecuteClassCleanupAsync(harness.CreateContext());

        failure.Should().BeNull();
        harness.Events.Should().Equal("class");
    }

    public async Task ClassCleanup_FallsBackToTheExecutionContextOfTheAssembly_WhenTheClassHasNone()
    {
        using var harness = new CleanupHarness();
        harness.Class.ClassCleanupMethod = Method(nameof(CleanupFixture.CleanupClassObservingTheContext));
        harness.Class.IsClassInitializeExecuted = true;
        harness.Assembly.ExecutionContext = CaptureWith("assembly");
        CleanupFixture.Marker.Value = "caller";
        TestFailedException? failure;
        try
        {
            failure = await harness.Class.ExecuteClassCleanupAsync(harness.CreateContext());
        }
        finally
        {
            CleanupFixture.Marker.Value = null;
        }

        failure.Should().BeNull();
        harness.Events.Should().Equal("assembly");
    }

    private static MethodInfo Method(string name) => typeof(CleanupFixture).GetMethod(name)!;

    private static SemaphoreSlim LockOf(TestClassInfo info)
        => (SemaphoreSlim)typeof(TestClassInfo).GetField("_testClassExecuteSyncSemaphore", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(info)!;

    private static TimeoutInfo ZeroCooperativeTimeout()
        => TimeoutInfo.FromTimeoutAttribute(new TimeoutAttribute(0) { CooperativeCancellation = true });

    private static async Task<int> CountDeclaringTypeReadsAsync(bool tracing)
    {
        using var harness = new CleanupHarness();
        var cleanup = new DeclaringTypeCountingMethod(Method(nameof(CleanupFixture.CleanupClass)));
        harness.Class.ClassCleanupMethod = cleanup;
        harness.Class.IsClassInitializeExecuted = true;
        if (tracing)
        {
            MSTestInstrumentation.SetActivityFactory((_, _) => new ActivityProbe());
        }

        TestFailedException? failure = await harness.Class.ExecuteClassCleanupAsync(harness.CreateContext());

        failure.Should().BeNull();
        harness.Events.Should().Equal("class");
        return cleanup.DeclaringTypeReads;
    }

    private static ExecutionContext CaptureWith(string value)
    {
        CleanupFixture.Marker.Value = value;
        try
        {
            return ExecutionContext.Capture()!;
        }
        finally
        {
            CleanupFixture.Marker.Value = null;
        }
    }

    private sealed class CleanupHarness : IDisposable
    {
        private readonly IPlatformServiceProvider _previous;

        internal CleanupHarness()
        {
            _previous = PlatformServiceProvider.Instance;
            Services = new TestablePlatformServiceProvider();
            Services.SetReflectionOperations(new ReflectionOperations());
            PlatformServiceProvider.Instance = Services;
            Assembly = new TestAssemblyInfo(typeof(CleanupFixture).Assembly);
            Class = new TestClassInfo(typeof(CleanupFixture), typeof(CleanupFixture).GetConstructor(Type.EmptyTypes), true, new TestClassAttribute(), Assembly);
        }

        internal List<string> Events { get; } = [];

        internal TestablePlatformServiceProvider Services { get; }

        internal TestClassInfo Class { get; }

        internal TestAssemblyInfo Assembly { get; }

        internal TestContextImplementation CreateContext()
            => new(null, typeof(CleanupFixture).FullName, new Dictionary<string, object?> { ["events"] = Events }, Mock.Of<IAdapterMessageLogger>(), null);

        public void Dispose()
        {
            MSTestInstrumentation.SetActivityFactory(null);
            MSTestSettings.Reset();
            PlatformServiceProvider.Instance = _previous;
        }
    }

    private sealed class DeclaringTypeCountingMethod : MethodInfo
    {
        private readonly MethodInfo _inner;

        internal DeclaringTypeCountingMethod(MethodInfo inner) => _inner = inner;

        internal int DeclaringTypeReads { get; private set; }

        public override Type? DeclaringType
        {
            get
            {
                DeclaringTypeReads++;
                return _inner.DeclaringType;
            }
        }

        public override ICustomAttributeProvider ReturnTypeCustomAttributes => _inner.ReturnTypeCustomAttributes;

        public override MethodAttributes Attributes => _inner.Attributes;

        public override RuntimeMethodHandle MethodHandle => _inner.MethodHandle;

        public override string Name => _inner.Name;

        public override Type? ReflectedType => _inner.ReflectedType;

        public override Type ReturnType => _inner.ReturnType;

        public override MethodInfo GetBaseDefinition() => _inner.GetBaseDefinition();

        public override MethodImplAttributes GetMethodImplementationFlags() => _inner.GetMethodImplementationFlags();

        public override ParameterInfo[] GetParameters() => _inner.GetParameters();

        public override object? Invoke(object? obj, BindingFlags invokeAttr, Binder? binder, object?[]? parameters, CultureInfo? culture)
            => _inner.Invoke(obj, invokeAttr, binder, parameters, culture);

        public override object[] GetCustomAttributes(bool inherit) => _inner.GetCustomAttributes(inherit);

        public override object[] GetCustomAttributes(Type attributeType, bool inherit) => _inner.GetCustomAttributes(attributeType, inherit);

        public override bool IsDefined(Type attributeType, bool inherit) => _inner.IsDefined(attributeType, inherit);
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

    public sealed class CleanupFixture
    {
        internal static readonly AsyncLocal<string?> Marker = new();

        public static void FirstBaseCleanup(TestContext context) => Record(context, "first-base");

        public static void SecondBaseCleanup(TestContext context) => Record(context, "second-base");

        public static void CleanupClass(TestContext context) => Record(context, "class");

        public static void CleanupClassObservingTheContext(TestContext context) => Record(context, Marker.Value ?? "none");

        public static void CleanupClassObservingTheThread(TestContext context)
        {
            Thread thread = Thread.CurrentThread;
            Record(context, $"pool={thread.IsThreadPoolThread},sta={thread.GetApartmentState() == ApartmentState.STA}");
        }

        private static void Record(TestContext context, string entry) => ((List<string>)context.Properties["events"]!).Add(entry);
    }
}
