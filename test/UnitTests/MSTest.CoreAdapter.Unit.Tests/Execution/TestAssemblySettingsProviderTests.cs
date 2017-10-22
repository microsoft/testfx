// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution
{
    extern alias FrameworkV1;
    extern alias FrameworkV2;
    extern alias FrameworkV2CoreExtension;

    using System;
    using System.Reflection;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
    using Moq;
    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestCleanup = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestCleanupAttribute;
    using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
    using UTF = FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class TestAssemblySettingsProviderTests
    {
        private TestablePlatformServiceProvider testablePlatformServiceProvider;
        private Mock<ReflectHelper> mockReflectHelper;
        private TestAssemblySettingsProvider testAssemblySettingProvider;

        [TestInitialize]
        public void TestInit()
        {
            this.testablePlatformServiceProvider = new TestablePlatformServiceProvider();
            this.testablePlatformServiceProvider.SetupMockReflectionOperations();

            PlatformServiceProvider.Instance = this.testablePlatformServiceProvider;

            this.mockReflectHelper = new Mock<ReflectHelper>();
            this.testAssemblySettingProvider = new TestAssemblySettingsProvider(this.mockReflectHelper.Object);
        }

        [TestCleanup]
        public void Cleanup()
        {
            PlatformServiceProvider.Instance = null;
        }

        [TestMethod]
        public void GetSettingsShouldSetParallelLevelToNegativeByDefault()
        {
            // Arrange.
            this.testablePlatformServiceProvider
                .MockFileOperations
                .Setup(fo => fo.LoadAssembly(It.IsAny<string>(), false))
                .Returns(Assembly.GetExecutingAssembly());

            // Act.
            var settings = this.testAssemblySettingProvider.GetSettings("Foo");

            // Assert.
            Assert.AreEqual(-1, settings.ParallelLevel);
        }

        [TestMethod]
        public void GetSettingsShouldSetParallelLevel()
        {
            // Arrange.
            this.testablePlatformServiceProvider
                .MockFileOperations
                .Setup(fo => fo.LoadAssembly(It.IsAny<string>(), false))
                .Returns(Assembly.GetExecutingAssembly());
            this.testablePlatformServiceProvider
                .MockReflectionOperations
                .Setup(ro => ro.GetCustomAttributes(It.IsAny<Assembly>(), typeof(UTF.TestParallelizationLevelAttribute)))
                .Returns(new[] { new UTF.TestParallelizationLevelAttribute(10) });

            // Act.
            var settings = this.testAssemblySettingProvider.GetSettings("Foo");

            // Assert.
            Assert.AreEqual(10, settings.ParallelLevel);
        }

        [TestMethod]
        public void GetSettingsShouldSetParallelLevelToProcessorCountIfZero()
        {
            // Arrange.
            this.testablePlatformServiceProvider
                .MockFileOperations
                .Setup(fo => fo.LoadAssembly(It.IsAny<string>(), false))
                .Returns(Assembly.GetExecutingAssembly());
            this.testablePlatformServiceProvider
                .MockReflectionOperations
                .Setup(ro => ro.GetCustomAttributes(It.IsAny<Assembly>(), typeof(UTF.TestParallelizationLevelAttribute)))
                .Returns(new[] { new UTF.TestParallelizationLevelAttribute(0) });

            // Act.
            var settings = this.testAssemblySettingProvider.GetSettings("Foo");

            // Assert.
            Assert.AreEqual(Environment.ProcessorCount, settings.ParallelLevel);
        }

        [TestMethod]
        public void GetSettingsShouldSetParallelModeToMethodLevelByDefault()
        {
            // Arrange.
            this.testablePlatformServiceProvider
                .MockFileOperations
                .Setup(fo => fo.LoadAssembly(It.IsAny<string>(), false))
                .Returns(Assembly.GetExecutingAssembly());

            // Act.
            var settings = this.testAssemblySettingProvider.GetSettings("Foo");

            // Assert.
            Assert.AreEqual(UTF.TestParallelizationMode.MethodLevel, settings.ParallelMode);
        }

        [TestMethod]
        public void GetSettingsShouldSetParallelMode()
        {
            // Arrange.
            this.testablePlatformServiceProvider
                .MockFileOperations
                .Setup(fo => fo.LoadAssembly(It.IsAny<string>(), false))
                .Returns(Assembly.GetExecutingAssembly());
            this.testablePlatformServiceProvider
                .MockReflectionOperations
                .Setup(ro => ro.GetCustomAttributes(It.IsAny<Assembly>(), typeof(UTF.TestParallelizationModeAttribute)))
                .Returns(new[] { new UTF.TestParallelizationModeAttribute(UTF.TestParallelizationMode.ClassLevel) });

            // Act.
            var settings = this.testAssemblySettingProvider.GetSettings("Foo");

            // Assert.
            Assert.AreEqual(UTF.TestParallelizationMode.ClassLevel, settings.ParallelMode);
        }

        [TestMethod]
        public void GetSettingsShouldSetCanParallelizeAssemblyToTrueByDefault()
        {
            // Arrange.
            this.testablePlatformServiceProvider
                .MockFileOperations
                .Setup(fo => fo.LoadAssembly(It.IsAny<string>(), false))
                .Returns(Assembly.GetExecutingAssembly());

            // Act.
            var settings = this.testAssemblySettingProvider.GetSettings("Foo");

            // Assert.
            Assert.AreEqual(true, settings.CanParallelizeAssembly);
        }

        [TestMethod]
        public void GetSettingsShouldSetCanParallelizeAssemblyToFalseIfDoNotParallelizeIsSet()
        {
            // Arrange.
            this.testablePlatformServiceProvider
                .MockFileOperations
                .Setup(fo => fo.LoadAssembly(It.IsAny<string>(), false))
                .Returns(Assembly.GetExecutingAssembly());
            this.testablePlatformServiceProvider
                .MockReflectionOperations
                .Setup(ro => ro.GetCustomAttributes(It.IsAny<Assembly>(), typeof(UTF.DoNotParallelizeAttribute)))
                .Returns(new[] { new UTF.DoNotParallelizeAttribute() });

            // Act.
            var settings = this.testAssemblySettingProvider.GetSettings("Foo");

            // Assert.
            Assert.AreEqual(false, settings.CanParallelizeAssembly);
        }
    }
}
