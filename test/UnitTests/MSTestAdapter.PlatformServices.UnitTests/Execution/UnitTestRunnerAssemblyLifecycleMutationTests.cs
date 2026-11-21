// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public sealed class UnitTestRunnerAssemblyLifecycleMutationTests : TestContainer
{
    private readonly IPlatformServiceProvider _previousProvider = PlatformServiceProvider.Instance;
    private readonly TestablePlatformServiceProvider _provider = new();

    public UnitTestRunnerAssemblyLifecycleMutationTests()
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

    public async Task AssemblyInitializeFailureResultCarriesTheMessagesWrittenByTheInitializeMethod()
    {
        TestResult[] results = await RunProbeAsync(
            assemblyInitialize: context =>
            {
                context.WriteLine("assembly-init-diagnostic");
                throw new InvalidOperationException("assembly-init-failure");
            },
            assemblyCleanup: null);

        TestResult result = results.Should().ContainSingle("the failed initialization replaces the result of the test").Subject;
        result.Outcome.Should().Be(UnitTestOutcome.Failed);
        NormalizeLineEndings(result.TestContextMessages).Should().Be("assembly-init-diagnostic\n");
    }

    public async Task AssemblyCleanupObservesThePropertiesSetByAssemblyInitialize()
    {
        string? observed = null;

        TestResult[] results = await RunProbeAsync(
            assemblyInitialize: context => context.Properties["assembly-init-property"] = "assembly-init-value",
            assemblyCleanup: context => observed = context.Properties.TryGetValue("assembly-init-property", out object? value) ? value as string : null);

        results.Should().ContainSingle().Which.Outcome.Should().Be(UnitTestOutcome.Passed);
        observed.Should().Be("assembly-init-value", "the properties captured after AssemblyInitialize flow into the AssemblyCleanup context");
    }

    public async Task AssemblyCleanupFailureResultCarriesTheMessagesWrittenByTheCleanupMethod()
    {
        TestResult[] results = await RunProbeAsync(
            assemblyInitialize: null,
            assemblyCleanup: context =>
            {
                context.WriteLine("assembly-cleanup-diagnostic");
                throw new InvalidOperationException("assembly-cleanup-failure");
            });

        results.Should().HaveCount(2, "the cleanup failure is reported next to the result of the test");
        results[0].Outcome.Should().Be(UnitTestOutcome.Passed);
        results[1].Outcome.Should().Be(UnitTestOutcome.Failed);
        NormalizeLineEndings(results[1].TestContextMessages).Should().Be("assembly-cleanup-diagnostic\n");
    }

    public async Task AssemblyCleanupMessagesAreAppendedToTheLastResultOfTheRun()
    {
        TestResult[] results = await RunProbeAsync(
            assemblyInitialize: null,
            assemblyCleanup: context => context.WriteLine("assembly-cleanup-diagnostic"));

        TestResult result = results.Should().ContainSingle().Subject;
        result.Outcome.Should().Be(UnitTestOutcome.Passed);
        NormalizeLineEndings(result.TestContextMessages).Should().Be("assembly-cleanup-diagnostic\n");
    }

    private static string? NormalizeLineEndings(string? text)
        => text?.Replace("\r\n", "\n");

    private static MSTestSettings CreateSettings(TestOutputCaptureMode captureMode)
    {
        var settings = new MSTestSettings();
        typeof(MSTestSettings)
            .GetProperty(nameof(MSTestSettings.OutputCaptureMode), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(settings, captureMode);
        return settings;
    }

    private async Task<TestResult[]> RunProbeAsync(Action<TestContext>? assemblyInitialize, Action<TestContext>? assemblyCleanup)
    {
        Type type = typeof(LifecycleProbe);
        MethodInfo test = type.GetMethod(nameof(LifecycleProbe.Test))!;
        var methodAttributes = new Dictionary<MethodInfo, Attribute[]> { [test] = [new TestMethodAttribute()] };
        var invokers = new Dictionary<MethodInfo, Func<object?, object?[]?, object?>> { [test] = (_, _) => Task.CompletedTask };

        if (assemblyInitialize is not null)
        {
            MethodInfo initialize = type.GetMethod(nameof(LifecycleProbe.AssemblyInitialize))!;
            methodAttributes[initialize] = [new AssemblyInitializeAttribute()];
            invokers[initialize] = (_, arguments) =>
            {
                assemblyInitialize((TestContext)arguments![0]!);
                return Task.CompletedTask;
            };
        }

        if (assemblyCleanup is not null)
        {
            MethodInfo cleanup = type.GetMethod(nameof(LifecycleProbe.AssemblyCleanup))!;
            methodAttributes[cleanup] = [new AssemblyCleanupAttribute()];
            invokers[cleanup] = (_, arguments) =>
            {
                assemblyCleanup((TestContext)arguments![0]!);
                return Task.CompletedTask;
            };
        }

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
                    [type] = [new() { Invoker = _ => new LifecycleProbe() }],
                },
            }));

        const string Source = "assembly-lifecycle-source";
        _provider.MockFileOperations.Setup(value => value.LoadAssembly(Source)).Returns(type.Assembly);

        var element = new UnitTestElement(new TestMethod(test.Name, null, test.Name, type.FullName!, Source, null, null));
        var runner = new UnitTestRunner(CreateSettings(TestOutputCaptureMode.None), [element]);
        IAdapterMessageLogger logger = new Mock<IAdapterMessageLogger>().Object;

        return await runner.RunSingleTestAsync(element, new Dictionary<string, object?>(), logger);
    }

    private sealed class LifecycleProbe
    {
        // The lifecycle attributes live only in the per-case metadata. Real AssemblyInitialize/AssemblyCleanup
        // attributes here would pollute other runner tests that inspect this unit-test assembly with ordinary reflection.
        public LifecycleProbe()
        {
        }

        public static void AssemblyInitialize(TestContext context)
        {
        }

        public static void AssemblyCleanup(TestContext context)
        {
        }

        public void Test()
        {
        }
    }
}
