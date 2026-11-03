// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using TestFramework.ForTestingMSTest;

using ExecutionScope = Microsoft.VisualStudio.TestTools.UnitTesting.ExecutionScope;

namespace MSTestAdapter.PlatformServices.UnitTests.Execution;

public sealed class ExecutionManagerContinuationTests : TestContainer
{
    public async Task MethodAndClassScopesHaveDifferentChunkBoundaries()
    {
        foreach (ExecutionScope scope in new[] { ExecutionScope.MethodLevel, ExecutionScope.ClassLevel })
        {
            using var harness = new ManagerHarness(scope);
            UnitTestElement[] tests =
            [
                harness.Element<FixtureA>(nameof(FixtureA.A1)),
                harness.Element<FixtureB>(nameof(FixtureB.B1)),
                harness.Element<FixtureA>(nameof(FixtureA.A2)),
                harness.Element<FixtureB>(nameof(FixtureB.B2)),
                harness.Element<FixtureC>(nameof(FixtureC.C1)),
            ];
            tests[4].DoNotParallelize = true;

            await harness.RunAsync(tests);

            harness.State.Sequence.Should().Equal(scope == ExecutionScope.MethodLevel
                ? new[] { "A1", "B1", "A2", "B2", "C1" }
                : new[] { "A1", "A2", "B1", "B2", "C1" });
            harness.Results.Select(result => result.Outcome).Should().OnlyContain(outcome => outcome == UnitTestOutcome.Passed);
            harness.Results.Should().HaveCount(5);
        }
    }

    public async Task ResourceLockedChunksCannotOverlapBeforeAnIndependentProbeReleasesTheGate()
    {
        foreach (bool graph in new[] { false, true })
        {
            using var harness = new ManagerHarness(ExecutionScope.MethodLevel);
            var release = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            int active = 0;
            int maximum = 0;
            harness.State.Body = async (name, _) =>
            {
                if (name == "C1")
                {
                    release.TrySetResult(null);
                    return;
                }

                int current = Interlocked.Increment(ref active);
                int observed;
                do
                {
                    observed = Volatile.Read(ref maximum);
                }
                while (current > observed && Interlocked.CompareExchange(ref maximum, current, observed) != observed);
                try
                {
                    if (name == "A1")
                    {
                        await release.Task.ConfigureAwait(false);
                    }
                }
                finally
                {
                    Interlocked.Decrement(ref active);
                }
            };
            UnitTestElement first = harness.Element<FixtureA>(nameof(FixtureA.A1));
            UnitTestElement second = harness.Element<FixtureB>(nameof(FixtureB.B1));
            UnitTestElement probe = harness.Element<FixtureC>(nameof(FixtureC.C1));
            first.ResourceLocks = [new("shared-fixture", ResourceAccessMode.ReadWrite)];
            second.ResourceLocks = [new("shared-fixture", ResourceAccessMode.ReadWrite)];
            if (graph)
            {
                second.Dependencies = [new(typeof(FixtureC).FullName, nameof(FixtureC.C1), false)];
            }

            try
            {
                await harness.RunAsync([first, second, probe]);
            }
            finally
            {
                release.TrySetResult(null);
            }

            maximum.Should().Be(1);
            active.Should().Be(0);
            harness.Results.Should().HaveCount(3);
            harness.Results.Select(result => result.Outcome).Should().OnlyContain(outcome => outcome == UnitTestOutcome.Passed);
        }
    }

    public async Task ParallelWorkerFailureLogsAndRethrowsTheOriginalCause()
    {
        foreach (bool graph in new[] { false, true })
        {
            using var harness = new ManagerHarness(ExecutionScope.MethodLevel);
            var cause = new InvalidOperationException("host start failed");
            UnitTestElement first = harness.Element<FixtureA>(nameof(FixtureA.A1));
            UnitTestElement second = harness.Element<FixtureB>(nameof(FixtureB.B1));
            if (graph)
            {
                second.Dependencies = [new(typeof(FixtureA).FullName, nameof(FixtureA.A1), false)];
            }

            harness.Recorder.Setup(value => value.RecordStartAsync(It.Is<UnitTestElement>(element => element.TestMethod.Name == "A1"))).Throws(cause);
            harness.Services.MockTraceLogger.SetupGet(value => value.IsErrorEnabled).Returns(true);
            List<(string Format, object?[] Arguments)> traceErrors = [];
            harness.Services.MockTraceLogger.Setup(value => value.Error(It.IsAny<string>(), It.IsAny<object?[]>()))
                .Callback<string, object?[]>((format, arguments) => traceErrors.Add((format, arguments)));
            Func<Task> run = () => harness.RunAsync([first, second]);

            (await run.Should().ThrowExactlyAsync<InvalidOperationException>()).Which.Should().BeSameAs(cause);
            harness.Messages.Where(message => message.Level == MessageLevel.Error).Should().ContainSingle()
                .Which.Text.Should().Contain("host start failed");
            traceErrors.Should().ContainSingle();
            traceErrors[0].Format.Should().Be("Error occurred while executing tests in parallel{0}{1}");
            traceErrors[0].Arguments[0].Should().Be(Environment.NewLine);
            traceErrors[0].Arguments[1].Should().BeOfType<string>().Which.Should().Contain(
                "System.InvalidOperationException: host start failed").And.Contain(nameof(ITestResultRecorder.RecordStartAsync));
            if (graph)
            {
                harness.State.Sequence.Should().BeEmpty();
                harness.Results.Should().ContainSingle().Which.Outcome.Should().Be(UnitTestOutcome.Ignored);
            }
            else
            {
                harness.State.Sequence.Should().Equal("B1");
            }
        }
    }

    public async Task SeededClassChunksRemainAtomicAndRepeatable()
    {
        List<string[]> orders = [];
        for (int attempt = 0; attempt < 2; attempt++)
        {
            using var harness = new ManagerHarness(ExecutionScope.ClassLevel, randomize: true);
            await harness.RunAsync(
            [
                harness.Element<FixtureA>(nameof(FixtureA.A1)),
                harness.Element<FixtureB>(nameof(FixtureB.B1)),
                harness.Element<FixtureA>(nameof(FixtureA.A2)),
                harness.Element<FixtureB>(nameof(FixtureB.B2)),
                harness.Element<FixtureC>(nameof(FixtureC.C1)),
            ]);
            string[] order = harness.State.Sequence.ToArray();
            order.Should().BeEquivalentTo(new[] { "A1", "A2", "B1", "B2", "C1" });
            Math.Abs(Array.IndexOf(order, "A1") - Array.IndexOf(order, "A2")).Should().Be(1);
            Math.Abs(Array.IndexOf(order, "B1") - Array.IndexOf(order, "B2")).Should().Be(1);
            orders.Add(order);
        }

        orders[1].Should().Equal(orders[0]);
    }

    public async Task PropertyPrecedenceAndDuplicateNamesPreserveTheDeclaredContract()
    {
        using var harness = new ManagerHarness(ExecutionScope.MethodLevel);
        harness.SourceParameters["collision"] = "source";
        harness.SourceParameters["source-only"] = 17;
        UnitTestElement element = harness.Element<FixtureA>(nameof(FixtureA.A1));
        element.ExecutionContextProperties = new Dictionary<string, object?> { ["collision"] = "tcm", ["tcm-only"] = "retained" };
        element.Traits = [new("collision", "trait"), new("custom", "first"), new("custom", "second"), new(string.Empty, "ignored")];
        element.TestCategory = ["custom", "category", string.Empty];
        await harness.RunAsync([element], "<RunSettings><TestRunParameters><Parameter name='collision' value='session'/><Parameter name='session-only' value='session'/></TestRunParameters></RunSettings>");

        Dictionary<string, object?> observed = harness.State.Snapshots.Single();
        observed["collision"].Should().Be("source");
        observed["source-only"].Should().Be(17);
        observed["tcm-only"].Should().Be("retained");
        observed["session-only"].Should().Be("session");
        observed["custom"].Should().Be("first");
        observed["category"].Should().Be(string.Empty);
        observed.Should().NotContainKey(string.Empty);
        ((Dictionary<string, object?>)element.ExecutionContextProperties)["collision"].Should().Be("tcm");
    }

    public async Task SessionParameterFailuresDoNotSilentlyClearThePreviousSession()
    {
        using var harness = new ManagerHarness(ExecutionScope.MethodLevel);
        await harness.RunAsync(
            [harness.Element<FixtureA>(nameof(FixtureA.A1))],
            "<RunSettings><TestRunParameters><Parameter name='retained' value='old'/></TestRunParameters></RunSettings>");
        await harness.RunAsync([harness.Element<FixtureA>(nameof(FixtureA.A1))], "<RunSettings><TestRunParameters>");
        await harness.RunAsync([harness.Element<FixtureA>(nameof(FixtureA.A1))], "<RunSettings><TestRunParameters /></RunSettings>");

        Dictionary<string, object?>[] snapshots = harness.State.Snapshots.ToArray();
        snapshots[0]["retained"].Should().Be("old");
        snapshots[1]["retained"].Should().Be("old");
        snapshots[2].Should().NotContainKey("retained");
        harness.Messages.Where(message => message.Level == MessageLevel.Error).Should().ContainSingle()
            .Which.Text.Should().Contain("TestRunParameters");
    }

    public async Task FilterFailureStopsBeforeRunnerCreation()
    {
        using var harness = new ManagerHarness(ExecutionScope.MethodLevel);
        var filter = new Mock<ITestElementFilterProvider>();
        bool invalid = true;
        filter.Setup(value => value.GetTestElementFilter(It.IsAny<IAdapterMessageLogger>(), out invalid)).Returns((ITestElementFilter?)null);

        await harness.RunAsync([harness.Element<FixtureA>(nameof(FixtureA.A1))], filter: filter.Object);

        harness.State.Sequence.Should().BeEmpty();
        harness.Results.Should().BeEmpty();
        harness.Services.MockTestSourceHost.Verify(value => value.CreateInstanceForType(typeof(UnitTestRunner), It.IsAny<object?[]>()), Times.Never);
        harness.Services.MockTestSourceHost.Verify(value => value.Dispose(), Times.Once);
    }

    public async Task SourceSettingsActivationFailureUsesTheConfiguredFallback()
    {
        using var harness = new ManagerHarness(ExecutionScope.MethodLevel);
        harness.Services.MockTraceLogger.SetupGet(value => value.IsInfoEnabled).Returns(true);
        var cause = new InvalidOperationException("source settings unavailable");
        harness.Services.MockTestSourceHost.Setup(value => value.CreateInstanceForType(typeof(TestAssemblySettingsProvider), null)).Throws(cause);

        await harness.RunAsync([harness.Element<FixtureA>(nameof(FixtureA.A1))]);

        harness.State.Sequence.Should().Equal("A1");
        harness.Results.Should().ContainSingle().Which.Outcome.Should().Be(UnitTestOutcome.Passed);
        harness.Services.MockTraceLogger.Verify(
            value => value.Info(
            "Could not create TestAssemblySettingsProvider instance in child app-domain", cause), Times.Once);
    }

    public async Task DefaultWorkerFactoryHonorsTheHostApartmentContract()
    {
        using var harness = new ManagerHarness(ExecutionScope.MethodLevel, useDefaultFactory: true);
        var apartments = new ConcurrentQueue<ApartmentState>();
        harness.State.Body = (_, _) =>
        {
            apartments.Enqueue(Thread.CurrentThread.GetApartmentState());
            return Task.CompletedTask;
        };

        await harness.RunAsync(
        [
            harness.Element<FixtureA>(nameof(FixtureA.A1)),
            harness.Element<FixtureB>(nameof(FixtureB.B1)),
            harness.Element<FixtureC>(nameof(FixtureC.C1)),
        ]);

        apartments.Should().HaveCount(3);
        ApartmentState expected = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ApartmentState.STA : ApartmentState.Unknown;
        apartments.Should().OnlyContain(apartment => apartment == expected);
        harness.Results.Should().HaveCount(3);
        harness.Results.Select(result => result.Outcome).Should().OnlyContain(outcome => outcome == UnitTestOutcome.Passed);
    }

    private sealed class ManagerHarness : IDisposable
    {
        private const string Source = "manager-source";
        private readonly IPlatformServiceProvider _previous;
        private readonly TestExecutionManager _manager;
        private readonly Mock<IAdapterMessageLogger> _logger = new();

        internal ManagerHarness(ExecutionScope scope, bool randomize = false, bool useDefaultFactory = false)
        {
            _previous = PlatformServiceProvider.Instance;
            Services = new TestablePlatformServiceProvider();
            PlatformServiceProvider.Instance = Services;
            _manager = useDefaultFactory ? new TestExecutionManager() : new TestExecutionManager(work => work());
            MSTestSettings.PopulateSettings(
                $"<RunSettings><RunConfiguration><ExecutionThreadApartmentState>{(useDefaultFactory ? "STA" : "MTA")}</ExecutionThreadApartmentState></RunConfiguration><MSTest><CaptureTraceOutput>None</CaptureTraceOutput><Parallelize><Workers>3</Workers><Scope>{scope}</Scope></Parallelize><RandomizeTestOrder>{randomize.ToString().ToLowerInvariant()}</RandomizeTestOrder><RandomTestOrderSeed>17</RandomTestOrderSeed></MSTest></RunSettings>",
                null, null);
            Services.SetReflectionOperations(new SourceGeneratedReflectionOperations(new SourceGeneratedReflectionDataProvider
            {
                Assembly = typeof(FixtureA).Assembly,
                AssemblyName = typeof(FixtureA).Assembly.GetName().Name!,
                Types = [typeof(FixtureA), typeof(FixtureB), typeof(FixtureC)],
                AssemblyAttributes = [new ParallelizeAttribute { Workers = 3, Scope = scope }],
            }));
            Services.MockFileOperations.Setup(value => value.LoadAssembly(Source)).Returns(typeof(FixtureA).Assembly);
            SourceParameters = new Dictionary<string, object> { ["state"] = State };
            Services.MockSettingsProvider.Setup(value => value.GetProperties(Source)).Returns(SourceParameters);
            Services.MockTestSourceHost.Setup(value => value.CreateInstanceForType(It.IsAny<Type>(), It.IsAny<object?[]?>()))
                .Returns<Type, object?[]?>((type, arguments) => type == typeof(TestAssemblySettingsProvider)
                    ? new TestAssemblySettingsProvider()
                    : type == typeof(UnitTestRunner)
                        ? new UnitTestRunner((MSTestSettings)arguments![0]!, (UnitTestElement[])arguments[1]!)
                        : throw new InvalidOperationException("Unexpected isolation activation: " + type));
            Recorder.Setup(value => value.RecordStartAsync(It.IsAny<UnitTestElement>())).Returns(Task.CompletedTask);
            Recorder.Setup(value => value.RecordEmptyResultAsync(It.IsAny<UnitTestElement>())).Returns(Task.CompletedTask);
            Recorder.Setup(value => value.RecordResultAsync(It.IsAny<UnitTestElement>(), It.IsAny<TestResult>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>()))
                .Callback<UnitTestElement, TestResult, DateTimeOffset, DateTimeOffset>((element, result, _, _) => Results.Enqueue((element.TestMethod.Name, result.Outcome)))
                .ReturnsAsync(false);
            _logger.Setup(value => value.SendMessage(It.IsAny<MessageLevel>(), It.IsAny<string>()))
                .Callback<MessageLevel, string>((level, text) => Messages.Enqueue((level, text)));
        }

        internal TestablePlatformServiceProvider Services { get; }

        internal RunState State { get; } = new();

        internal Dictionary<string, object> SourceParameters { get; }

        internal Mock<ITestResultRecorder> Recorder { get; } = new();

        internal ConcurrentQueue<(string Name, UnitTestOutcome Outcome)> Results { get; } = new();

        internal ConcurrentQueue<(MessageLevel Level, string Text)> Messages { get; } = new();

        internal UnitTestElement Element<T>(string method)
            => new(new TestMethod(method, null, method, typeof(T).FullName!, Source, method, null)
            {
                MethodInfo = typeof(T).GetMethod(method),
            });

        internal Task RunAsync(UnitTestElement[] elements, string? xml = null, ITestElementFilterProvider? filter = null)
            => _manager.RunTestsAsync(elements, new DeploymentContext(null, xml), _logger.Object, Recorder.Object, filter, new TestRunCancellationToken());

        public void Dispose()
        {
            MSTestSettings.Reset();
            PlatformServiceProvider.Instance = _previous;
        }
    }

    public sealed class RunState
    {
        internal ConcurrentQueue<string> Sequence { get; } = new();

        internal ConcurrentQueue<Dictionary<string, object?>> Snapshots { get; } = new();

        internal Func<string, TestContext, Task>? Body { get; set; }

        internal Task ExecuteAsync(string name, TestContext context)
        {
            Sequence.Enqueue(name);
            Snapshots.Enqueue([with(context.Properties)]);
            return Body?.Invoke(name, context) ?? Task.CompletedTask;
        }
    }

    public abstract class FixtureBase
    {
        public TestContext TestContext { get; set; } = null!;

        protected Task ExecuteAsync(string name) => ((RunState)TestContext.Properties["state"]!).ExecuteAsync(name, TestContext);
    }

    [TestClass]
    public sealed class FixtureA : FixtureBase
    {
        [TestMethod]
        public Task A1() => ExecuteAsync("A1");

        [TestMethod]
        public Task A2() => ExecuteAsync("A2");
    }

    [TestClass]
    public sealed class FixtureB : FixtureBase
    {
        [TestMethod]
        public Task B1() => ExecuteAsync("B1");

        [TestMethod]
        public Task B2() => ExecuteAsync("B2");
    }

    [TestClass]
    public sealed class FixtureC : FixtureBase
    {
        [TestMethod]
        public Task C1() => ExecuteAsync("C1");
    }
}
