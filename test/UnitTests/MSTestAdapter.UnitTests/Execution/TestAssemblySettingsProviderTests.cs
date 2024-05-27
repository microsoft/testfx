// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;

using TestFramework.ForTestingMSTest;

using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

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
        MSTest.TestAdapter.ObjectModel.TestAssemblySettings settings = TestAssemblySettingsProvider.GetSettings("Foo");

        // Assert.
        Verify(settings.Workers == -1);
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
            .Setup(ro => ro.GetCustomAttributes(It.IsAny<Assembly>(), typeof(UTF.ParallelizeAttribute)))
            .Returns([new UTF.ParallelizeAttribute { Workers = 10 }]);

        // Act.
        MSTest.TestAdapter.ObjectModel.TestAssemblySettings settings = TestAssemblySettingsProvider.GetSettings("Foo");

        // Assert.
        Verify(settings.Workers == 10);
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
            .Setup(ro => ro.GetCustomAttributes(It.IsAny<Assembly>(), typeof(UTF.ParallelizeAttribute)))
            .Returns([new UTF.ParallelizeAttribute { Workers = 0 }]);

        // Act.
        MSTest.TestAdapter.ObjectModel.TestAssemblySettings settings = TestAssemblySettingsProvider.GetSettings("Foo");

        // Assert.
        Verify(Environment.ProcessorCount == settings.Workers);
    }

    public void GetSettingsShouldSetParallelScopeToClassLevelByDefault()
    {
        // Arrange.
        _testablePlatformServiceProvider
            .MockFileOperations
            .Setup(fo => fo.LoadAssembly(It.IsAny<string>(), false))
            .Returns(Assembly.GetExecutingAssembly());

        // Act.
        MSTest.TestAdapter.ObjectModel.TestAssemblySettings settings = TestAssemblySettingsProvider.GetSettings("Foo");

        // Assert.
        Verify(settings.Scope == UTF.ExecutionScope.ClassLevel);
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
            .Setup(ro => ro.GetCustomAttributes(It.IsAny<Assembly>(), typeof(UTF.ParallelizeAttribute)))
            .Returns([new UTF.ParallelizeAttribute { Scope = UTF.ExecutionScope.MethodLevel }]);

        // Act.
        MSTest.TestAdapter.ObjectModel.TestAssemblySettings settings = TestAssemblySettingsProvider.GetSettings("Foo");

        // Assert.
        Verify(settings.Scope == UTF.ExecutionScope.MethodLevel);
    }

    public void GetSettingsShouldSetCanParallelizeAssemblyToTrueByDefault()
    {
        // Arrange.
        _testablePlatformServiceProvider
            .MockFileOperations
            .Setup(fo => fo.LoadAssembly(It.IsAny<string>(), false))
            .Returns(Assembly.GetExecutingAssembly());

        // Act.
        MSTest.TestAdapter.ObjectModel.TestAssemblySettings settings = TestAssemblySettingsProvider.GetSettings("Foo");

        // Assert.
        Verify(settings.CanParallelizeAssembly);
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
            .Setup(ro => ro.GetCustomAttributes(It.IsAny<Assembly>(), typeof(UTF.DoNotParallelizeAttribute)))
            .Returns([new UTF.DoNotParallelizeAttribute()]);

        // Act.
        MSTest.TestAdapter.ObjectModel.TestAssemblySettings settings = TestAssemblySettingsProvider.GetSettings("Foo");

        // Assert.
        Verify(!settings.CanParallelizeAssembly);
    }
}
