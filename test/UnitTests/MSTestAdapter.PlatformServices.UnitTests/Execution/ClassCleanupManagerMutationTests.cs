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

public sealed class ClassCleanupManagerMutationTests : TestContainer
{
    public async Task ForceCleanup_WhenTheClassHasNoClassInitialize_FlowsTheAssemblyInitializeProperties()
    {
        using var harness = new CleanupHarness();
        (await harness.Assembly.RunAssemblyInitializeAsync(harness.CreateContext())).Outcome.Should().Be(UnitTestOutcome.Passed);

        // The fixture has no [ClassInitialize], so nothing is snapshotted into PostClassInitProperties:
        // the assembly properties can only reach the cleanup context through the assembly-level merge.
        (await harness.Class.GetResultOrRunClassInitializeAsync(harness.CreateContext(), null, null, null, null)).Outcome.Should().Be(UnitTestOutcome.Passed);
        harness.Class.PostClassInitProperties.Should().BeNull();
        harness.Assembly.PostAssemblyInitProperties.Should().ContainKey("assembly");

        ClassCleanupManager.ForceCleanup(harness.Cache, harness.SourceParameters, Mock.Of<IAdapterMessageLogger>());

        harness.Events.Should().Equal("class-cleanup:A");
        harness.Class.IsClassCleanupExecuted.Should().BeTrue();
    }

    private static MethodInfo Method(string name) => typeof(CleanupOnlyFixture).GetMethod(name)!;

    private sealed class CleanupHarness : IDisposable
    {
        private readonly IPlatformServiceProvider _previous;

        internal CleanupHarness()
        {
            _previous = PlatformServiceProvider.Instance;
            var services = new TestablePlatformServiceProvider();
            services.SetReflectionOperations(new ReflectionOperations());
            services.MockFileOperations.Setup(value => value.LoadAssembly("cleanup-source")).Returns(typeof(CleanupOnlyFixture).Assembly);
            PlatformServiceProvider.Instance = services;
            SourceParameters = new Dictionary<string, object?> { ["events"] = Events };

            var reflection = new Mock<ReflectHelper>();
            reflection.Setup(value => value.IsAttributeDefined<TestClassAttribute>(typeof(CleanupOnlyFixture))).Returns(true);
            reflection.Setup(value => value.IsAttributeDefined<ClassCleanupAttribute>(Method(nameof(CleanupOnlyFixture.CleanupClass)))).Returns(true);
            reflection.Setup(value => value.IsAttributeDefined<AssemblyInitializeAttribute>(Method(nameof(CleanupOnlyFixture.InitializeAssembly)))).Returns(true);
            Cache = new TypeCache(reflection.Object);
            _ = Cache.GetTestMethodInfo(new TestMethod(
                nameof(CleanupOnlyFixture.Test), null, nameof(CleanupOnlyFixture.Test), typeof(CleanupOnlyFixture).FullName!, "cleanup-source", null, null));
            Class = Cache.ClassInfoCache.Single()!;
            Assembly = Cache.AssemblyInfoCache.Single();
        }

        internal List<string> Events { get; } = [];

        internal Dictionary<string, object?> SourceParameters { get; }

        internal TypeCache Cache { get; }

        internal TestClassInfo Class { get; }

        internal TestAssemblyInfo Assembly { get; }

        internal TestContextImplementation CreateContext()
            => new(null, typeof(CleanupOnlyFixture).FullName, new Dictionary<string, object?> { ["events"] = Events }, Mock.Of<IAdapterMessageLogger>(), null);

        public void Dispose()
        {
            MSTestSettings.Reset();
            PlatformServiceProvider.Instance = _previous;
        }
    }

    [TestClass]
    public sealed class CleanupOnlyFixture
    {
        public void Test()
        {
        }

        public static void InitializeAssembly(TestContext context)
            => context.Properties["assembly"] = "A";

        public static void CleanupClass(TestContext context)
        {
            var events = (List<string>)context.Properties["events"]!;
            context.Properties.TryGetValue("assembly", out object? assembly);
            events.Add($"class-cleanup:{assembly ?? "-"}");
        }
    }
}
