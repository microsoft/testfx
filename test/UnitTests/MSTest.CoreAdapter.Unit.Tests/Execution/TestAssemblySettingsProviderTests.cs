﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

using System;
using System.Reflection;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Moq;

using TestFramework.ForTestingMSTest;

using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

public class TestAssemblySettingsProviderTests : TestContainer
{
    private TestablePlatformServiceProvider _testablePlatformServiceProvider;
    private Mock<ReflectHelper> _mockReflectHelper;
    private TestAssemblySettingsProvider _testAssemblySettingProvider;

    [TestInitialize]
    public void TestInit()
    {
        _testablePlatformServiceProvider = new TestablePlatformServiceProvider();
        _testablePlatformServiceProvider.SetupMockReflectionOperations();

        PlatformServiceProvider.Instance = _testablePlatformServiceProvider;

        _mockReflectHelper = new Mock<ReflectHelper>();
        _testAssemblySettingProvider = new TestAssemblySettingsProvider(_mockReflectHelper.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        PlatformServiceProvider.Instance = null;
    }

    public void GetSettingsShouldSetParallelWorkersToNegativeByDefault()
    {
        // Arrange.
        _testablePlatformServiceProvider
            .MockFileOperations
            .Setup(fo => fo.LoadAssembly(It.IsAny<string>(), false))
            .Returns(Assembly.GetExecutingAssembly());

        // Act.
        var settings = TestAssemblySettingsProvider.GetSettings("Foo");

        // Assert.
        Assert.AreEqual(-1, settings.Workers);
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
            .Returns(new[] { new UTF.ParallelizeAttribute { Workers = 10 } });

        // Act.
        var settings = TestAssemblySettingsProvider.GetSettings("Foo");

        // Assert.
        Assert.AreEqual(10, settings.Workers);
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
            .Returns(new[] { new UTF.ParallelizeAttribute { Workers = 0 } });

        // Act.
        var settings = TestAssemblySettingsProvider.GetSettings("Foo");

        // Assert.
        Assert.AreEqual(Environment.ProcessorCount, settings.Workers);
    }

    public void GetSettingsShouldSetParallelScopeToClassLevelByDefault()
    {
        // Arrange.
        _testablePlatformServiceProvider
            .MockFileOperations
            .Setup(fo => fo.LoadAssembly(It.IsAny<string>(), false))
            .Returns(Assembly.GetExecutingAssembly());

        // Act.
        var settings = TestAssemblySettingsProvider.GetSettings("Foo");

        // Assert.
        Assert.AreEqual(UTF.ExecutionScope.ClassLevel, settings.Scope);
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
            .Returns(new[] { new UTF.ParallelizeAttribute { Scope = UTF.ExecutionScope.MethodLevel } });

        // Act.
        var settings = TestAssemblySettingsProvider.GetSettings("Foo");

        // Assert.
        Assert.AreEqual(UTF.ExecutionScope.MethodLevel, settings.Scope);
    }

    public void GetSettingsShouldSetCanParallelizeAssemblyToTrueByDefault()
    {
        // Arrange.
        _testablePlatformServiceProvider
            .MockFileOperations
            .Setup(fo => fo.LoadAssembly(It.IsAny<string>(), false))
            .Returns(Assembly.GetExecutingAssembly());

        // Act.
        var settings = TestAssemblySettingsProvider.GetSettings("Foo");

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
            .Returns(new[] { new UTF.DoNotParallelizeAttribute() });

        // Act.
        var settings = TestAssemblySettingsProvider.GetSettings("Foo");

        // Assert.
        Verify(!settings.CanParallelizeAssembly);
    }
}
