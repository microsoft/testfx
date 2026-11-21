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
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;

using TestFramework.ForTestingMSTest;

using ITestMethod = Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ObjectModel.ITestMethod;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public sealed class UnitTestRunnerRunSingleTestMutationTests : TestContainer
{
    private const string Source = "run-single-test-source";

    private readonly IPlatformServiceProvider _previousProvider = PlatformServiceProvider.Instance;
    private readonly CapturingPlatformServiceProvider _provider = new();

    public UnitTestRunnerRunSingleTestMutationTests()
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

    public async Task RunSingleTestAsyncRejectsANullTestElement()
    {
        ProbeRun run = CreateProbeRun();

        // The other arguments are valid so that only the element guard can reject the call.
        Func<Task> act = () => run.RunAsync(null, new Dictionary<string, object?>(), new Dictionary<string, object?>());

        (await act.Should().ThrowExactlyAsync<ArgumentNullException>()).WithParameterName("unitTestElement");
    }

    public async Task RunSingleTestAsyncRejectsNullTestContextProperties()
    {
        ProbeRun run = CreateProbeRun();

#if DEBUG && !NETFRAMEWORK
        // Without its guard the runner would hand the null dictionary to the test context, whose constructor asserts
        // on it; an unobserved failed assertion terminates the test host.
        List<string?> assertions = [];
        using var observer = new DiagnosticAssertionObserver(assertions.Add);
#endif

        // The lifecycle properties are valid so that only the test context properties guard can reject the call.
        Func<Task> act = () => run.RunAsync(run.Element(0), null, new Dictionary<string, object?>());

        (await act.Should().ThrowExactlyAsync<ArgumentNullException>()).WithParameterName("testContextProperties");
#if DEBUG && !NETFRAMEWORK
        assertions.Should().BeEmpty("because the guard rejects the call before any diagnostic assertion can fire");
#endif
    }

    public async Task RunSingleTestAsyncDisposesEveryContextItCreates()
    {
        ProbeRun run = CreateProbeRun(
            assemblyInitialize: _ => { },
            classInitialize: _ => { },
            classCleanup: _ => { },
            assemblyCleanup: _ => { });

        await RunPassingAsync(run, 0);

        CancelAndDescribeContexts(0).Should().Equal(
            ["test:disposed", "assembly:disposed", "class:disposed", "class:disposed", "assembly:disposed"],
            "because a disposed context is no longer linked to the run cancellation token");
    }

    public async Task RunSingleTestAsyncFlowsTheAssemblyAndClassInitializePropertiesToTheClassInitializeTheTestAndTheClassCleanup()
    {
        List<string> observed = [];
        ProbeRun run = CreateProbeRun(
            assemblyInitialize: context => context.Properties["assembly-key"] = "assembly-value",
            classInitialize: context =>
            {
                observed.Add(Describe("class initialize", context, "assembly-key"));
                context.Properties["class-key"] = "class-value";
            },
            testBody: context => observed.Add(Describe("test", context, "class-key")),
            classCleanup: context => observed.Add(Describe("class cleanup", context, "class-key")));

        await RunPassingAsync(run, 0);

        observed.Should().Equal(
            [
                "class initialize sees assembly-key=assembly-value",
                "test sees class-key=class-value",
                "class cleanup sees class-key=class-value",
            ]);
    }

    public async Task RunSingleTestAsyncFlowsTheAssemblyInitializePropertiesToTheTestAndTheClassCleanupWhenThereIsNoClassInitialize()
    {
        // Without a [ClassInitialize] no class-level snapshot exists, so the runner's own merges are the only way
        // the AssemblyInitialize properties can reach the test and the class cleanup.
        List<string> observed = [];
        ProbeRun run = CreateProbeRun(
            assemblyInitialize: context => context.Properties["assembly-key"] = "assembly-value",
            testBody: context => observed.Add(Describe("test", context, "assembly-key")),
            classCleanup: context => observed.Add(Describe("class cleanup", context, "assembly-key")));

        await RunPassingAsync(run, 0);

        observed.Should().Equal(
            [
                "test sees assembly-key=assembly-value",
                "class cleanup sees assembly-key=assembly-value",
            ]);
    }

    public async Task RunSingleTestAsyncHandsTheClassInitializeOutcomeToTheTest()
    {
        UnitTestOutcome? observed = null;
        ProbeRun run = CreateProbeRun(
            classInitialize: context => ((TestContextImplementation)context).SetOutcome(UnitTestOutcome.Inconclusive),
            testBody: context => observed = context.CurrentTestOutcome);

        await RunPassingAsync(run, 0);

        observed.Should().Be(UnitTestOutcome.Inconclusive, "because the outcome of the class initialize context is handed to the context of the test");
    }

#if DEBUG && !NETFRAMEWORK
    public async Task RunSingleTestAsyncAssertsThatTheClassInitializeWasExecutedBeforeUsingItsCachedResult()
    {
        ProbeRun run = CreateProbeRun(testCount: 2, classInitialize: _ => { });
        await RunPassingAsync(run, 0);
        run.ClassInfo().IsClassInitializeExecuted.Should().BeTrue("because the first test ran the class initialize");

        // The cached result is only trusted together with the flag that records that the class initialize ran.
        run.ClassInfo().IsClassInitializeExecuted = false;
        List<string?> assertions = [];
        using var observer = new DiagnosticAssertionObserver(assertions.Add);

        await RunPassingAsync(run, 1);

        assertions.Should().Equal("IsClassInitializeExecuted should be true after attempting to run it.");
    }
#endif

    public async Task RunSingleTestAsyncFailsEveryTestOfTheAssemblyWhenAssemblyInitializeFailed()
    {
        int testBodyCount = 0;
        ProbeRun run = CreateProbeRun(
            testCount: 2,
            assemblyInitialize: _ => throw new InvalidOperationException("assembly initialize failure"),
            testBody: _ => testBodyCount++);

        TestResult[] first = await run.RunAsync(0);
        TestResult[] second = await run.RunAsync(1);

        first.Should().ContainSingle().Which.Outcome.Should().Be(UnitTestOutcome.Failed);
        second.Should().ContainSingle("because the cached AssemblyInitialize failure is reported again").Which.Outcome.Should().Be(UnitTestOutcome.Failed);
        testBodyCount.Should().Be(0, "because no test may run once its assembly failed to initialize");
    }

    public async Task RunSingleTestAsyncRetriesOnlyTheTestsThatDidNotPass()
    {
        (string Description, bool Fails, int ExpectedInvocations, UnitTestOutcome[] ExpectedOutcomes)[] cases =
        [
            ("a passing test", false, 1, [UnitTestOutcome.Passed]),
            ("a failing test", true, 3, [UnitTestOutcome.Failed, UnitTestOutcome.Failed, UnitTestOutcome.Failed]),
        ];

        foreach ((string description, bool fails, int expectedInvocations, UnitTestOutcome[] expectedOutcomes) in cases)
        {
            int invocations = 0;
            ProbeRun run = CreateProbeRun(
                retryAttempts: 2,
                testBody: _ =>
                {
                    invocations++;
                    if (fails)
                    {
                        throw new InvalidOperationException("test failure");
                    }
                });

            TestResult[] results = await run.RunAsync(0);

            invocations.Should().Be(expectedInvocations, "because of {0}", description);
            results.Select(result => result.Outcome).Should().Equal(expectedOutcomes, "because of {0}", description);
        }
    }

    public void AlignAttemptNumberCarriesTheAttemptOfTheLastResultThatIsNotSuperseded()
    {
        // The results given to the method end with the final attempt, which is never superseded, so the loop can
        // only be told apart from its mutations with arrays that the runner does not produce itself.
        (string Description, TestResult[] Preceding, int Expected)[] cases =
        [
            ("a final attempt after a superseded one", [Attempt(1, isSuperseded: true), Attempt(2, isSuperseded: false)], 2),
            ("a superseded attempt after the final one", [Attempt(3, isSuperseded: false), Attempt(1, isSuperseded: true)], 3),
            ("two results that are not superseded", [Attempt(2, isSuperseded: false), Attempt(3, isSuperseded: false)], 3),
        ];
        MethodInfo align = typeof(UnitTestRunner).GetMethod("AlignAttemptNumber", BindingFlags.Static | BindingFlags.NonPublic)!;

        foreach ((string description, TestResult[] preceding, int expected) in cases)
        {
            var cleanupResult = new TestResult();

            var aligned = (TestResult)align.Invoke(null, [cleanupResult, preceding])!;

            aligned.Should().BeSameAs(cleanupResult, "because of {0}", description);
            aligned.RetryAttemptNumber.Should().Be(expected, "because of {0}", description);
        }
    }

    private static TestResult Attempt(int attemptNumber, bool isSuperseded)
        => new() { RetryAttemptNumber = attemptNumber, IsSupersededRetryAttempt = isSuperseded };

    private static string Describe(string phase, TestContext context, string key)
        => $"{phase} sees {key}={ReadProperty(context, key) ?? "<absent>"}";

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

        MethodInfo method = typeof(RunProbe).GetMethod(methodName)!;
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
        int testCount = 1,
        Action<TestContext>? assemblyInitialize = null,
        Action<TestContext>? classInitialize = null,
        Action<TestContext>? classCleanup = null,
        Action<TestContext>? assemblyCleanup = null,
        Action<TestContext>? testBody = null,
        int retryAttempts = 0)
    {
        Type type = typeof(RunProbe);
        var methodAttributes = new Dictionary<MethodInfo, Attribute[]>();
        var invokers = new Dictionary<MethodInfo, Func<object?, object?[]?, object?>>();
        var elements = new UnitTestElement[testCount];
        for (int i = 0; i < elements.Length; i++)
        {
            string name = $"Test{i + 1}";
            MethodInfo test = type.GetMethod(name)!;
            methodAttributes[test] = retryAttempts > 0
                ? [new TestMethodAttribute(), new RetryAttribute(retryAttempts)]
                : [new TestMethodAttribute()];
            invokers[test] = (_, _) =>
            {
                testBody?.Invoke(TestContext.Current!);
                return Task.CompletedTask;
            };
            elements[i] = new UnitTestElement(new TestMethod(name, null, name, type.FullName!, Source, null, null));
        }

        AddLifecycleMethod(methodAttributes, invokers, nameof(RunProbe.AssemblyInitialize), new AssemblyInitializeAttribute(), assemblyInitialize);
        AddLifecycleMethod(methodAttributes, invokers, nameof(RunProbe.ClassInitialize), new ClassInitializeAttribute(), classInitialize);
        AddLifecycleMethod(methodAttributes, invokers, nameof(RunProbe.ClassCleanup), new ClassCleanupAttribute(), classCleanup);
        AddLifecycleMethod(methodAttributes, invokers, nameof(RunProbe.AssemblyCleanup), new AssemblyCleanupAttribute(), assemblyCleanup);

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
                    [type] = [new() { Invoker = _ => new RunProbe() }],
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

#if DEBUG && !NETFRAMEWORK
        public TestClassInfo ClassInfo()
        {
            var typeCache = (TypeCache)typeof(UnitTestRunner).GetField("_typeCache", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_runner)!;
            return typeCache.ClassInfoCache.Single()!;
        }
#endif

        public Task<TestResult[]> RunAsync(int index)
            => RunAsync(_elements[index], new Dictionary<string, object?>(), new Dictionary<string, object?>());

        public Task<TestResult[]> RunAsync(UnitTestElement? element, IDictionary<string, object?>? testContextProperties, IDictionary<string, object?>? lifecycleContextProperties)
            => _runner.RunSingleTestAsync(element!, testContextProperties!, lifecycleContextProperties!, _logger);
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

    private sealed class RunProbe
    {
        public RunProbe()
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
    }
}
