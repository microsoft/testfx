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
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

#if DEBUG && !NETFRAMEWORK
using Moq;
#endif

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public sealed class TestAssemblyInfoMutationTests : TestContainer
{
    private readonly IPlatformServiceProvider _previousProvider = PlatformServiceProvider.Instance;
    private readonly TestablePlatformServiceProvider _provider = new();

    public TestAssemblyInfoMutationTests()
    {
        PlatformServiceProvider.Instance = _provider;
        MSTestInstrumentation.SetActivityFactory(null);
    }

    protected override void Dispose(bool disposing)
    {
        MSTestInstrumentation.SetActivityFactory(null);
        PlatformServiceProvider.Instance = _previousProvider;
        base.Dispose(disposing);
    }

    public void DiscoversInternalsIsTrueWhenTheAssemblyHasTheDiscoverInternalsAttribute()
    {
        var info = new TestAssemblyInfo(new TestableAssembly(new DiscoverInternalsAttribute()));

        info.DiscoversInternals.Should().BeTrue();
    }

    public void DiscoversInternalsIsFalseWhenTheAssemblyHasNoDiscoverInternalsAttribute()
    {
        var info = new TestAssemblyInfo(new TestableAssembly());

        info.DiscoversInternals.Should().BeFalse();
    }

    public async Task RunAssemblyInitializeAsyncStartsAnActivityDescribingTheAssemblyInitializeFixture()
    {
        List<RecordedActivity> activities = InstallActivityFactory();
        var info = new TestAssemblyInfo(new TestableAssembly()) { AssemblyInitializeMethod = GetFixtureMethod(nameof(Fixtures.AssemblyInitialize)) };
        using TestContextImplementation testContext = CreateTestContext();

        TestResult result = await info.RunAssemblyInitializeAsync(testContext);

        result.Outcome.Should().Be(UnitTestOutcome.Passed);
        RecordedActivity activity = activities.Should().ContainSingle().Which;
        activity.Name.Should().Be("MSTest.AssemblyInitialize");
        activity.Tags.Should().Equal(new Dictionary<string, object?>
        {
            ["test.fixture.kind"] = "assembly_initialize",
            ["test.suite.name"] = typeof(Fixtures).FullName,
            ["test.assembly.name"] = "TestableAssembly",
        });
        activity.RecordedExceptions.Should().BeEmpty();
        activity.IsDisposed.Should().BeTrue();
    }

    public async Task RunAssemblyInitializeAsyncDoesNotInspectTheAssemblyWhenTracingIsOff()
    {
        var assembly = new TestableAssembly();
        var info = new TestAssemblyInfo(assembly) { AssemblyInitializeMethod = GetFixtureMethod(nameof(Fixtures.AssemblyInitialize)) };
        using TestContextImplementation testContext = CreateTestContext();

        TestResult result = await info.RunAssemblyInitializeAsync(testContext);

        result.Outcome.Should().Be(UnitTestOutcome.Passed);
        assembly.GetNameCalls.Should().Be(0);
    }

    public async Task RunAssemblyInitializeAsyncRecordsTheInitializationFailureOnTheActivity()
    {
        List<RecordedActivity> activities = InstallActivityFactory();
        var info = new TestAssemblyInfo(new TestableAssembly()) { AssemblyInitializeMethod = GetFixtureMethod(nameof(Fixtures.FailingAssemblyInitialize)) };
        using TestContextImplementation testContext = CreateTestContext();

        TestResult result = await info.RunAssemblyInitializeAsync(testContext);

        result.Outcome.Should().Be(UnitTestOutcome.Failed);
        info.AssemblyInitializationException.Should().NotBeNull();
        result.TestFailureException.Should().BeSameAs(info.AssemblyInitializationException!);
        RecordedActivity activity = activities.Should().ContainSingle().Which;
        activity.RecordedExceptions.Should().ContainSingle().Which.Should().BeSameAs(info.AssemblyInitializationException!);
    }

    public async Task ExecuteAssemblyCleanupAsyncStartsAnActivityDescribingTheAssemblyCleanupFixture()
    {
        List<RecordedActivity> activities = InstallActivityFactory();
        var info = new TestAssemblyInfo(new TestableAssembly()) { AssemblyCleanupMethod = GetFixtureMethod(nameof(Fixtures.AssemblyCleanup)) };
        using TestContextImplementation testContext = CreateTestContext();

        TestFailedException? result = await info.ExecuteAssemblyCleanupAsync(testContext);

        result.Should().BeNull();
        RecordedActivity activity = activities.Should().ContainSingle().Which;
        activity.Name.Should().Be("MSTest.AssemblyCleanup");
        activity.Tags.Should().Equal(new Dictionary<string, object?>
        {
            ["test.fixture.kind"] = "assembly_cleanup",
            ["test.suite.name"] = typeof(Fixtures).FullName,
            ["test.assembly.name"] = "TestableAssembly",
        });
        activity.RecordedExceptions.Should().BeEmpty();
        activity.IsDisposed.Should().BeTrue();
    }

    public async Task ExecuteAssemblyCleanupAsyncDoesNotInspectTheAssemblyWhenTracingIsOff()
    {
        var assembly = new TestableAssembly();
        var info = new TestAssemblyInfo(assembly) { AssemblyCleanupMethod = GetFixtureMethod(nameof(Fixtures.AssemblyCleanup)) };
        using TestContextImplementation testContext = CreateTestContext();

        TestFailedException? result = await info.ExecuteAssemblyCleanupAsync(testContext);

        result.Should().BeNull();
        assembly.GetNameCalls.Should().Be(0);
    }

    public async Task ExecuteAssemblyCleanupAsyncReturnsAndRecordsTheOriginalFailureOfACanceledCleanup()
    {
#if DEBUG && !NETFRAMEWORK
        // A regression that rebuilds the failure would trip unrelated debug assertions; observe them so the
        // host reports the assertion failures below instead of failing fast.
        using var observer = new DiagnosticAssertionObserver(static _ => { });
#endif
        List<RecordedActivity> activities = InstallActivityFactory();
        var info = new TestAssemblyInfo(new TestableAssembly()) { AssemblyCleanupMethod = GetFixtureMethod(nameof(Fixtures.AssemblyCleanup)) };
        using TestContextImplementation testContext = CreateTestContext();
        testContext.CancellationTokenSource.Cancel();

        TestFailedException? result = await info.ExecuteAssemblyCleanupAsync(testContext);

        result.Should().NotBeNull();
        result!.Outcome.Should().Be(UnitTestOutcome.Timeout);
        result.Message.Should().Be($"Assembly cleanup method '{typeof(Fixtures).FullName}.{nameof(Fixtures.AssemblyCleanup)}' was canceled");
        result.Should().BeSameAs(info.AssemblyCleanupException!);
        RecordedActivity activity = activities.Should().ContainSingle().Which;
        activity.RecordedExceptions.Should().ContainSingle().Which.Should().BeSameAs(result);
    }

    public async Task ExecuteAssemblyCleanupAsyncReleasesTheExecutionSemaphore()
    {
        var info = new TestAssemblyInfo(new TestableAssembly()) { AssemblyCleanupMethod = GetFixtureMethod(nameof(Fixtures.AssemblyCleanup)) };
        using TestContextImplementation testContext = CreateTestContext();

        TestFailedException? result = await info.ExecuteAssemblyCleanupAsync(testContext);

        result.Should().BeNull();
        GetExecutionSemaphore(info).CurrentCount.Should().Be(1);
    }

#if DEBUG && !NETFRAMEWORK
    public void AssemblyInitializeMethodSetterAssertsThatTheMethodAlreadySetHasADeclaringType()
    {
        List<string?> assertions = [];
        using var observer = new DiagnosticAssertionObserver(assertions.Add);
        var info = new TestAssemblyInfo(new TestableAssembly());
        MethodInfo method = CreateMethodWithoutDeclaringType();
        info.AssemblyInitializeMethod = method;

        Action setAgain = () => info.AssemblyInitializeMethod = method;

        setAgain.Should().Throw<NullReferenceException>();
        assertions.Should().Equal("AssemblyInitializeMethod.DeclaringType.FullName is null");
    }

    public void AssemblyCleanupMethodSetterAssertsThatTheMethodAlreadySetHasADeclaringType()
    {
        List<string?> assertions = [];
        using var observer = new DiagnosticAssertionObserver(assertions.Add);
        var info = new TestAssemblyInfo(new TestableAssembly());
        MethodInfo method = CreateMethodWithoutDeclaringType();
        info.AssemblyCleanupMethod = method;

        Action setAgain = () => info.AssemblyCleanupMethod = method;

        setAgain.Should().Throw<NullReferenceException>();
        assertions.Should().Equal("AssemblyCleanupMethod.DeclaringType.FullName is null");
    }

    public async Task RunAssemblyInitializeAsyncAssertsThatTheFailingMethodHasADeclaringType()
    {
        List<string?> assertions = [];
        using var observer = new DiagnosticAssertionObserver(assertions.Add);
        var info = new TestAssemblyInfo(new TestableAssembly()) { AssemblyInitializeMethod = CreateFailingMethodWithoutDeclaringType() };
        using TestContextImplementation testContext = CreateTestContext();

        Func<Task> run = () => info.RunAssemblyInitializeAsync(testContext);

        await run.Should().ThrowAsync<NullReferenceException>();
        assertions.Should().Equal("AssemblyInitializeMethod.DeclaringType.FullName is null");
    }

    public async Task ExecuteAssemblyCleanupAsyncAssertsThatTheFailingMethodHasADeclaringType()
    {
        List<string?> assertions = [];
        using var observer = new DiagnosticAssertionObserver(assertions.Add);
        var info = new TestAssemblyInfo(new TestableAssembly()) { AssemblyCleanupMethod = CreateFailingMethodWithoutDeclaringType() };
        using TestContextImplementation testContext = CreateTestContext();

        Func<Task> run = () => info.ExecuteAssemblyCleanupAsync(testContext);

        await run.Should().ThrowAsync<NullReferenceException>();
        assertions.Should().Equal("AssemblyCleanupMethod.DeclaringType.Name is null");
    }

    private static MethodInfo CreateMethodWithoutDeclaringType() => new Mock<MethodInfo>().Object;

    private static MethodInfo CreateFailingMethodWithoutDeclaringType()
    {
        var method = new Mock<MethodInfo>();
        method
            .Setup(m => m.Invoke(It.IsAny<object?>(), It.IsAny<BindingFlags>(), It.IsAny<Binder?>(), It.IsAny<object?[]?>(), It.IsAny<CultureInfo?>()))
            .Throws(new InvalidOperationException("boom"));
        return method.Object;
    }
#endif

    private static TestContextImplementation CreateTestContext() => new(null, null, new Dictionary<string, object?>(), null, null);

    private static MethodInfo GetFixtureMethod(string name) => typeof(Fixtures).GetMethod(name)!;

    private static SemaphoreSlim GetExecutionSemaphore(TestAssemblyInfo info)
        => (SemaphoreSlim)typeof(TestAssemblyInfo).GetField("_assemblyInfoExecuteSyncSemaphore", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(info)!;

    private static List<RecordedActivity> InstallActivityFactory()
    {
        List<RecordedActivity> activities = [];
        MSTestInstrumentation.SetActivityFactory((name, tags) =>
        {
            var activity = new RecordedActivity(name, tags);
            activities.Add(activity);
            return activity;
        });

        return activities;
    }

    private static class Fixtures
    {
        public static void AssemblyInitialize()
        {
        }

        public static void FailingAssemblyInitialize() => throw new InvalidOperationException("I fail.");

        public static void AssemblyCleanup()
        {
        }
    }

    private sealed class TestableAssembly(params Attribute[] attributes) : Assembly
    {
        public int GetNameCalls { get; private set; }

        public override AssemblyName GetName()
        {
            GetNameCalls++;
            return new AssemblyName("TestableAssembly");
        }

        // Attribute.GetCustomAttributes casts the result to Attribute[], so the runtime array type matters.
        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
            => attributes.Where(attributeType.IsInstanceOfType).ToArray();
    }

    private sealed class RecordedActivity(string name, IEnumerable<KeyValuePair<string, object?>>? tags) : IMSTestActivity
    {
        public string Name { get; } = name;

        public Dictionary<string, object?> Tags { get; } = tags?.ToDictionary(static tag => tag.Key, static tag => tag.Value) ?? [];

        public List<Exception?> RecordedExceptions { get; } = [];

        public bool IsDisposed { get; private set; }

        public void SetTag(string key, object? value) => Tags[key] = value;

        public void SetFailed(string? description)
        {
        }

        public void RecordException(Exception exception) => RecordedExceptions.Add(exception);

        public void Dispose() => IsDisposed = true;
    }
}
