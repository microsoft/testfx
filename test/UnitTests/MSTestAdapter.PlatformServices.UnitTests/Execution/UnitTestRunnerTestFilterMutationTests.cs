// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;

using TestFramework.ForTestingMSTest;

using ITestMethod = Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ObjectModel.ITestMethod;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public sealed class UnitTestRunnerTestFilterMutationTests : TestContainer
{
    private const string Source = "test-filter-source";

    private readonly IPlatformServiceProvider _previousProvider = PlatformServiceProvider.Instance;
    private readonly CapturingPlatformServiceProvider _provider = new();

    public UnitTestRunnerTestFilterMutationTests()
    {
        PlatformServiceProvider.Instance = _provider;
    }

    protected override void Dispose(bool disposing)
    {
        TestRun.SetCurrent(null);
        MSTestSettings.Reset();
        PlatformServiceProvider.Instance = _previousProvider;
        base.Dispose(disposing);
    }

    public async Task NotRunTestHandsTheAssemblyInitializePropertiesToTheClassCleanup()
    {
        // Without a [ClassInitialize] no class-level snapshot exists, so the runner's own merge is the only way
        // the AssemblyInitialize properties can reach the class cleanup.
        List<string?> observed = [];
        ProbeRun run = CreateProbeRun(
            assemblyInitialize: context => context.Properties["assembly-key"] = "assembly-value",
            classCleanup: context => observed.Add(ReadProperty(context, "assembly-key")));

        await RunPassingAsync(run, 0);
        await RunPassingAsync(run, 1);
        await run.NotRunAsync(2);

        observed.Should().Equal(["assembly-value"], "because the class cleanup triggered by a not-run test must see what AssemblyInitialize stored");
    }

    public async Task NotRunTestHandsTheClassInitializePropertiesToTheClassCleanup()
    {
        List<string?> observed = [];
        ProbeRun run = CreateProbeRun(
            classInitialize: context => context.Properties["class-key"] = "class-value",
            classCleanup: context => observed.Add(ReadProperty(context, "class-key")));

        await RunPassingAsync(run, 0);
        await RunPassingAsync(run, 1);
        await run.NotRunAsync(2);

        observed.Should().Equal(["class-value"], "because the class cleanup triggered by a not-run test must see what ClassInitialize stored");
    }

    public async Task NotRunTestReportsAFailedClassCleanupAgainstTheLastRunnableTest()
    {
        ProbeRun run = CreateProbeRun(classCleanup: _ => throw new InvalidOperationException("class cleanup failure"));

        await RunPassingAsync(run, 0);
        await RunPassingAsync(run, 1);
        TestResult[] results = await run.NotRunAsync(2);

        TestResult failure = results.Should().ContainSingle("because the failed class cleanup is the only result of the not-run test").Subject;
        failure.Outcome.Should().Be(UnitTestOutcome.Failed);
        failure.DisplayName.Should().Be($"[{typeof(FilterProbe).FullName} ClassCleanup]");
        failure.AssociatedUnitTestElement.Should().BeSameAs(run.Element(1), "because the cleanup is attributed to the last test of the class that actually ran");
    }

    public async Task NotRunTestReportsAFailedAssemblyCleanupAgainstTheLastRunnableTestOfTheAssembly()
    {
        ProbeRun run = CreateProbeRun(assemblyCleanup: _ => throw new InvalidOperationException("assembly cleanup failure"));

        await RunPassingAsync(run, 0);
        await RunPassingAsync(run, 1);
        TestResult[] results = await run.NotRunAsync(2);

        TestResult failure = results.Should().ContainSingle("because the failed assembly cleanup is the only result of the not-run test").Subject;
        failure.Outcome.Should().Be(UnitTestOutcome.Failed);
        failure.TestFailureException.Should().NotBeNull();
        failure.AssociatedUnitTestElement.Should().BeSameAs(run.Element(1), "because the cleanup is attributed to the last test of the assembly that actually ran");
    }

    public async Task NotRunTestDisposesTheContextsItCreates()
    {
        ProbeRun run = CreateProbeRun(classCleanup: _ => { }, assemblyCleanup: _ => { });

        await RunPassingAsync(run, 0);
        await RunPassingAsync(run, 1);
        int contextsBefore = _provider.Contexts.Count;
        await run.NotRunAsync(2);

        CancelAndDescribeContexts(contextsBefore).Should().Equal(
            ["test:disposed", "class:disposed", "assembly:disposed"],
            "because a disposed context is no longer linked to the run cancellation token");
    }

    public async Task NotRunTestRunsTheEndOfAssemblyCleanupOnlyOnceTheLastTestOfTheClassIsComplete()
    {
        int assemblyCleanupCount = 0;
        ProbeRun run = CreateProbeRun(assemblyCleanup: _ => assemblyCleanupCount++);

        await RunPassingAsync(run, 0);
        await RunPassingAsync(run, 1);
        assemblyCleanupCount.Should().Be(0, "because the class still has a test left to account for");

        await run.NotRunAsync(2);

        assemblyCleanupCount.Should().Be(1, "because the not-run test completes the last class, which releases the end-of-assembly cleanup");
    }

    public async Task NotRunTestDoesNotRunTheAssemblyCleanupWhenNoTestExecutedTheAssemblyInitialize()
    {
        int assemblyCleanupCount = 0;
        ProbeRun run = CreateProbeRun(assemblyCleanup: _ => assemblyCleanupCount++, ignoreFirstTest: true);

        TestResult[] ignored = await run.RunAsync(0);
        await run.NotRunAsync(1);
        await run.NotRunAsync(2);

        ignored.Should().ContainSingle("because the first test is ignored").Subject.Outcome.Should().Be(UnitTestOutcome.Ignored);
        assemblyCleanupCount.Should().Be(0, "because no test ever triggered the AssemblyInitialize this cleanup pairs with");
        _provider.Contexts.Select(recorded => recorded.Kind).Should().Equal(
            ["test", "test", "test"],
            "because no assembly-level context is needed when there is nothing to clean up");
    }

    public void CreateFilterContextOnlyReadsNamespaceAndClassFromAHierarchyThatReachesTheClassLevel()
    {
        (string?[] Hierarchy, string? Namespace, string? ClassName)[] cases =
        [
            (["assembly", "Namespace"], null, null),
            (["assembly", "Namespace", "Class"], "Namespace", "Class"),
        ];

        foreach ((string?[] hierarchy, string? expectedNamespace, string? expectedClassName) in cases)
        {
            var element = new UnitTestElement(new TestMethod(null, hierarchy, "Test", "Namespace.Class", "source", null, null));

            var context = (TestFilterContext)typeof(UnitTestRunner)
                .GetMethod("CreateFilterContext", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, [element])!;

            context.Namespace.Should().Be(expectedNamespace, "because of the hierarchy [{0}]", string.Join(", ", hierarchy));
            context.ClassName.Should().Be(expectedClassName, "because of the hierarchy [{0}]", string.Join(", ", hierarchy));
        }
    }

    private static string? ReadProperty(TestContext context, string key)
        => context.Properties.TryGetValue(key, out object? value) ? value as string : null;

    private static async Task RunPassingAsync(ProbeRun run, int index)
    {
        TestResult[] results = await run.RunAsync(index);

        TestResult result = results.Should().ContainSingle("because the arrangement runs the test normally").Subject;
        result.Outcome.Should().Be(UnitTestOutcome.Passed);
    }

    private static MSTestSettings CreateSettings(TestOutputCaptureMode captureMode)
    {
        var settings = new MSTestSettings();
        typeof(MSTestSettings)
            .GetProperty(nameof(MSTestSettings.OutputCaptureMode), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(settings, captureMode);
        return settings;
    }

    private static void AddLifecycleMethod(
        Dictionary<MethodInfo, Attribute[]> methodAttributes,
        Dictionary<MethodInfo, Func<object?, object?[]?, object?>> invokers,
        string methodName,
        Attribute attribute,
        Action<TestContext>? body)
    {
        if (body is null)
        {
            return;
        }

        MethodInfo method = typeof(FilterProbe).GetMethod(methodName)!;
        methodAttributes[method] = [attribute];
        invokers[method] = (_, arguments) =>
        {
            body((TestContext)arguments![0]!);
            return Task.CompletedTask;
        };
    }

    // The probe's lifecycle attributes are supplied per case through the source-generated metadata: real attributes on
    // the type would also be picked up by the other runner tests that inspect this assembly with ordinary reflection.
    private ProbeRun CreateProbeRun(
        Action<TestContext>? assemblyInitialize = null,
        Action<TestContext>? classInitialize = null,
        Action<TestContext>? classCleanup = null,
        Action<TestContext>? assemblyCleanup = null,
        bool ignoreFirstTest = false)
    {
        Type type = typeof(FilterProbe);
        var methodAttributes = new Dictionary<MethodInfo, Attribute[]>();
        var invokers = new Dictionary<MethodInfo, Func<object?, object?[]?, object?>>();
        var elements = new UnitTestElement[3];
        for (int i = 0; i < elements.Length; i++)
        {
            string name = $"Test{i + 1}";
            MethodInfo test = type.GetMethod(name)!;
            methodAttributes[test] = ignoreFirstTest && i == 0
                ? [new TestMethodAttribute(), new IgnoreAttribute("ignored by the test arrangement")]
                : [new TestMethodAttribute()];
            invokers[test] = (_, _) => Task.CompletedTask;
            elements[i] = new UnitTestElement(new TestMethod(name, null, name, type.FullName!, Source, null, null));
        }

        AddLifecycleMethod(methodAttributes, invokers, nameof(FilterProbe.AssemblyInitialize), new AssemblyInitializeAttribute(), assemblyInitialize);
        AddLifecycleMethod(methodAttributes, invokers, nameof(FilterProbe.ClassInitialize), new ClassInitializeAttribute(), classInitialize);
        AddLifecycleMethod(methodAttributes, invokers, nameof(FilterProbe.ClassCleanup), new ClassCleanupAttribute(), classCleanup);
        AddLifecycleMethod(methodAttributes, invokers, nameof(FilterProbe.AssemblyCleanup), new AssemblyCleanupAttribute(), assemblyCleanup);

        _provider.SetReflectionOperations(new SourceGeneratedReflectionOperations(
            new SourceGeneratedReflectionDataProvider
            {
                Assembly = type.Assembly,
                AssemblyName = type.Assembly.GetName().Name!,
                Types = [type],
                TypesByName = new Dictionary<string, Type> { [type.FullName!] = type },
                AssemblyAttributes = [],
                TypeAttributes = new Dictionary<Type, Attribute[]> { [type] = [new TestClassAttribute()] },
                TypeMethodAttributes = methodAttributes,
                TypeMethodInvokers = invokers,
                TypeConstructorsInvoker = new Dictionary<Type, SourceGeneratedReflectionDataProvider.ConstructorInvoker[]>
                {
                    [type] = [new() { Invoker = _ => new FilterProbe() }],
                },
            }));
        _provider.MockFileOperations.Setup(value => value.LoadAssembly(Source)).Returns(type.Assembly);

        return new ProbeRun(new UnitTestRunner(CreateSettings(TestOutputCaptureMode.None), elements), elements);
    }

    // Cancelling the shared token reaches exactly the contexts that were not disposed.
    private List<string> CancelAndDescribeContexts(int skip)
    {
        _provider.SharedToken.Cancel();

        List<string> descriptions = [];
        foreach (RecordedContext recorded in _provider.Contexts.Skip(skip))
        {
            string disposition = recorded.Context.Context.CancellationTokenSource.IsCancellationRequested ? "alive" : "disposed";
            descriptions.Add($"{recorded.Kind}:{disposition}");
        }

        return descriptions;
    }

    private sealed class ProbeRun
    {
        private readonly UnitTestRunner _runner;
        private readonly UnitTestElement[] _elements;
        private readonly IAdapterMessageLogger _logger = new Mock<IAdapterMessageLogger>().Object;

        public ProbeRun(UnitTestRunner runner, UnitTestElement[] elements)
        {
            _runner = runner;
            _elements = elements;
        }

        public UnitTestElement Element(int index)
            => _elements[index];

        public Task<TestResult[]> RunAsync(int index)
            => _runner.RunSingleTestAsync(_elements[index], new Dictionary<string, object?>(), _logger);

        public Task<TestResult[]> NotRunAsync(int index)
            => _runner.NotifyTestNotRunAsync(_elements[index], new Dictionary<string, object?>(), new Dictionary<string, object?>(), _logger);
    }

    private sealed class RecordedContext
    {
        public RecordedContext(string kind, TestContextImplementation context)
        {
            Kind = kind;
            Context = context;
        }

        public string Kind { get; }

        public TestContextImplementation Context { get; }
    }

    private sealed class CapturingPlatformServiceProvider : TestablePlatformServiceProvider, IPlatformServiceProvider
    {
        public List<RecordedContext> Contexts { get; } = [];

        public TestRunCancellationToken SharedToken { get; } = new();

        public new ITestContext GetTestContext(ITestMethod? testMethod, string? testClassFullName, IDictionary<string, object?> properties, IAdapterMessageLogger messageLogger, UnitTestOutcome outcome)
        {
            var context = new TestContextImplementation(testMethod, testClassFullName, properties, messageLogger, SharedToken);
            context.SetOutcome(outcome);

            string kind = testMethod is not null ? "test" : testClassFullName is null ? "assembly" : "class";
            Contexts.Add(new RecordedContext(kind, context));
            return context;
        }
    }

    private sealed class FilterProbe
    {
        public FilterProbe()
        {
        }

        public static void AssemblyInitialize(TestContext context)
        {
        }

        public static void ClassInitialize(TestContext context)
        {
        }

        public static void ClassCleanup(TestContext context)
        {
        }

        public static void AssemblyCleanup(TestContext context)
        {
        }

        public void Test1()
        {
        }

        public void Test2()
        {
        }

        public void Test3()
        {
        }
    }
}
