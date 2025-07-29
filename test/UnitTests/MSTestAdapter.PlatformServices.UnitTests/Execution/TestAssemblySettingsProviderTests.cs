// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Moq;
using TestFramework.ForTestingMSTest;

using ExecutionScope = Microsoft.VisualStudio.TestTools.UnitTesting.ExecutionScope;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public class TestAssemblySettingsProviderTests : TestContainer
{
    private readonly TestablePlatformServiceProvider _testablePlatformServiceProvider;

    public TestAssemblySettingsProviderTests()
    {
        _testablePlatformServiceProvider = new TestablePlatformServiceProvider();
        _testablePlatformServiceProvider.SetupMockReflectionOperations();

        PlatformServiceProvider.Instance = _testablePlatformServiceProvider;
    }

    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            base.Dispose(disposing);
            PlatformServiceProvider.Instance = null;
        }
    }

    public void GetSettingsShouldSetParallelWorkersToNegativeByDefault()
    {
        // Arrange.
        _testablePlatformServiceProvider
            .MockFileOperations
            .Setup(fo => fo.LoadAssembly(It.IsAny<string>(), false))
            .Returns(Assembly.GetExecutingAssembly());

        // Act.
        MSTest.TestAdapter.ObjectModel.TestAssemblySettings settings = new TestAssemblySettingsProvider().GetSettings("Foo");

        // Assert.
        settings.Workers.Should().Be(-1);
    }

    public void GetSettingsShouldSetParallelWorkers()
    {
        // Arrange.
        _testablePlatformServiceProvider
            .MockFileOperations
            .Setup(fo => fo.LoadAssembly(It.IsAny<string>(), false))
            .Returns(Assembly.GetExecutingAssembly());
        _testablePlatformServiceProvider
            .MockReflectionOperations
            .Setup(ro => ro.GetCustomAttributes(It.IsAny<Assembly>(), typeof(ParallelizeAttribute)))
            .Returns([new ParallelizeAttribute { Workers = 10 }]);

        // Act.
        MSTest.TestAdapter.ObjectModel.TestAssemblySettings settings = new TestAssemblySettingsProvider().GetSettings("Foo");

        // Assert.
        settings.Workers.Should().Be(10);
    }

    public void GetSettingsShouldSetParallelWorkersToProcessorCountIfZero()
    {
        // Arrange.
        _testablePlatformServiceProvider
            .MockFileOperations
            .Setup(fo => fo.LoadAssembly(It.IsAny<string>(), false))
            .Returns(Assembly.GetExecutingAssembly());
        _testablePlatformServiceProvider
            .MockReflectionOperations
            .Setup(ro => ro.GetCustomAttributes(It.IsAny<Assembly>(), typeof(ParallelizeAttribute)))
            .Returns([new ParallelizeAttribute { Workers = 0 }]);

        // Act.
        MSTest.TestAdapter.ObjectModel.TestAssemblySettings settings = new TestAssemblySettingsProvider().GetSettings("Foo");

        // Assert.
        Environment.ProcessorCount.Should().Be(settings.Workers);
    }

    public void GetSettingsShouldSetParallelScopeToClassLevelByDefault()
    {
        // Arrange.
        _testablePlatformServiceProvider
            .MockFileOperations
            .Setup(fo => fo.LoadAssembly(It.IsAny<string>(), false))
            .Returns(Assembly.GetExecutingAssembly());

        // Act.
        MSTest.TestAdapter.ObjectModel.TestAssemblySettings settings = new TestAssemblySettingsProvider().GetSettings("Foo");

        // Assert.
        settings.Scope.Should().Be(ExecutionScope.ClassLevel);
    }

    public void GetSettingsShouldSetParallelScope()
    {
        // Arrange.
        _testablePlatformServiceProvider
            .MockFileOperations
            .Setup(fo => fo.LoadAssembly(It.IsAny<string>(), false))
            .Returns(Assembly.GetExecutingAssembly());
        _testablePlatformServiceProvider
            .MockReflectionOperations
            .Setup(ro => ro.GetCustomAttributes(It.IsAny<Assembly>(), typeof(ParallelizeAttribute)))
            .Returns([new ParallelizeAttribute { Scope = ExecutionScope.MethodLevel }]);

        // Act.
        MSTest.TestAdapter.ObjectModel.TestAssemblySettings settings = new TestAssemblySettingsProvider().GetSettings("Foo");

        // Assert.
        settings.Scope.Should().Be(ExecutionScope.MethodLevel);
    }

    public void GetSettingsShouldSetCanParallelizeAssemblyToTrueByDefault()
    {
        // Arrange.
        _testablePlatformServiceProvider
            .MockFileOperations
            .Setup(fo => fo.LoadAssembly(It.IsAny<string>(), false))
            .Returns(Assembly.GetExecutingAssembly());

        // Act.
        MSTest.TestAdapter.ObjectModel.TestAssemblySettings settings = new TestAssemblySettingsProvider().GetSettings("Foo");

        // Assert.
        settings.CanParallelizeAssembly.Should().BeTrue();
    }

    public void GetSettingsShouldSetCanParallelizeAssemblyToFalseIfDoNotParallelizeIsSet()
    {
        // Arrange.
        _testablePlatformServiceProvider
            .MockFileOperations
            .Setup(fo => fo.LoadAssembly(It.IsAny<string>(), false))
            .Returns(Assembly.GetExecutingAssembly());
        _testablePlatformServiceProvider
            .MockReflectionOperations
            .Setup(ro => ro.GetCustomAttributes(It.IsAny<Assembly>(), typeof(DoNotParallelizeAttribute)))
            .Returns([new DoNotParallelizeAttribute()]);

        // Act.
        MSTest.TestAdapter.ObjectModel.TestAssemblySettings settings = new TestAssemblySettingsProvider().GetSettings("Foo");

        // Assert.
        !settings.CanParallelizeAssembly.Should().BeTrue();
    }
}
