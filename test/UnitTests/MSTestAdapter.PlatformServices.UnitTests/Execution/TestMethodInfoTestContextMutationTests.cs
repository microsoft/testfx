// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public sealed class TestMethodInfoTestContextMutationTests : TestContainer
{
    public void SetTestContextAssignsTheContextThroughTheSourceGeneratedSetterWhenOneIsRegistered()
    {
        var calls = new List<(object? Instance, object? Value)>();
        using var harness = new ContextHarness((instance, value) => calls.Add((instance, value)));
        var fixture = new ContextFixture();

        bool successful = harness.SetTestContext(fixture, new TestResult());

        successful.Should().BeTrue();
        calls.Should().ContainSingle();
        calls[0].Instance.Should().BeSameAs(fixture);
        calls[0].Value.Should().BeSameAs(harness.Context);
        fixture.TestContext.Should().BeNull(because: "the source generated setter replaces the reflection based assignment");
    }

    public void SetTestContextAssignsTheContextThroughReflectionWhenNoSourceGeneratedSetterIsRegistered()
    {
        using var harness = new ContextHarness(sourceGeneratedSetter: null);
        var fixture = new ContextFixture();

        bool successful = harness.SetTestContext(fixture, new TestResult());

        successful.Should().BeTrue();
        fixture.TestContext.Should().BeSameAs(harness.Context);
    }

    private sealed class ContextHarness : IDisposable
    {
        private static readonly PropertyInfo ContextProperty = typeof(ContextFixture).GetProperty(nameof(ContextFixture.TestContext))!;
        private static readonly MethodInfo SetTestContextMethod = typeof(TestMethodInfo).GetMethod("SetTestContext", BindingFlags.NonPublic | BindingFlags.Instance)!;

        private readonly IPlatformServiceProvider _previousProvider;
        private readonly TestMethodInfo _subject;

        public ContextHarness(Action<object?, object?>? sourceGeneratedSetter)
        {
            _previousProvider = PlatformServiceProvider.Instance;
            var services = new TestablePlatformServiceProvider();
            services.SetReflectionOperations(sourceGeneratedSetter is null
                ? new ReflectionOperations()
                : new SourceGeneratedReflectionOperations(new SourceGeneratedReflectionDataProvider
                {
                    TypePropertySetters = new Dictionary<PropertyInfo, Action<object?, object?>> { [ContextProperty] = sourceGeneratedSetter },
                }));
            PlatformServiceProvider.Instance = services;

            Context = new TestContextImplementation(null, typeof(ContextFixture).FullName, new Dictionary<string, object?>(), null, null);
            var assemblyInfo = new TestAssemblyInfo(typeof(ContextFixture).Assembly);
            var classInfo = new TestClassInfo(typeof(ContextFixture), typeof(ContextFixture).GetConstructor(Type.EmptyTypes), true, new TestClassAttribute(), assemblyInfo);
            _subject = new TestMethodInfo(typeof(ContextFixture).GetMethod(nameof(ContextFixture.TestMethod))!, classInfo) { TestContext = Context };
        }

        public TestContextImplementation Context { get; }

        public bool SetTestContext(object classInstance, TestResult result)
            => (bool)SetTestContextMethod.Invoke(_subject, [classInstance, result])!;

        public void Dispose()
            => PlatformServiceProvider.Instance = _previousProvider;
    }

    private sealed class ContextFixture
    {
        public TestContext? TestContext { get; set; }

        public void TestMethod()
        {
        }
    }
}
