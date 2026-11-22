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

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Execution;

public sealed class TestClassInfoPass2MutationTests : TestContainer
{
    public void SettingTheTestCleanupMethodTwice_ThrowsTheMultipleCleanupErrorAndKeepsTheFirstMethod()
    {
        using var harness = new CleanupHarness();
        MethodInfo first = typeof(CleanupFixture).GetMethod(nameof(CleanupFixture.First))!;
        MethodInfo second = typeof(CleanupFixture).GetMethod(nameof(CleanupFixture.Second))!;
        harness.Class.TestCleanupMethod = first;

        Action secondAssignment = () => harness.Class.TestCleanupMethod = second;

        secondAssignment.Should().Throw<TypeInspectionException>().Which.Message.Should().Be(string.Format(
            CultureInfo.CurrentCulture,
            Resource.UTA_ErrorMultiClean,
            typeof(CleanupFixture).FullName));
        harness.Class.TestCleanupMethod.Should().BeSameAs(first);
    }

    private sealed class CleanupHarness : IDisposable
    {
        private readonly IPlatformServiceProvider _previous;

        internal CleanupHarness()
        {
            _previous = PlatformServiceProvider.Instance;
            var services = new TestablePlatformServiceProvider();
            services.SetReflectionOperations(new ReflectionOperations());
            PlatformServiceProvider.Instance = services;
            Class = new TestClassInfo(
                typeof(CleanupFixture),
                typeof(CleanupFixture).GetConstructor(Type.EmptyTypes),
                true,
                new TestClassAttribute(),
                new TestAssemblyInfo(typeof(CleanupFixture).Assembly));
        }

        internal TestClassInfo Class { get; }

        public void Dispose()
        {
            MSTestSettings.Reset();
            PlatformServiceProvider.Instance = _previous;
        }
    }

    public sealed class CleanupFixture
    {
        public static void First()
        {
        }

        public static void Second()
        {
        }
    }
}
