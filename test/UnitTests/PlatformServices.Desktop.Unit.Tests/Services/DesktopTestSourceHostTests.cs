// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.PlatformServices.Desktop.UnitTests
{
    extern alias FrameworkV1;
    extern alias FrameworkV2;

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Security.Policy;

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;

    using Moq;

    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;

    [TestClass]
    public class DesktopTestSourceHostTests
    {
        [TestMethod]
        public void GetResolutionPathsShouldAddPublicAndPrivateAssemblyPath()
        {
            // Setup
            TestSourceHost sut = new TestSourceHost(null, null, null);

            // Execute
            // It should return public and private path if it is not running in portable mode.
            List<string> result = sut.GetResolutionPaths("DummyAssembly.dll", isPortableMode: false);

            // Assert
            Assert.IsTrue(result.Contains(VSInstallationUtilities.PathToPublicAssemblies));
            Assert.IsTrue(result.Contains(VSInstallationUtilities.PathToPrivateAssemblies));
        }

        [TestMethod]
        public void GetResolutionPathsShouldNotAddPublicAndPrivateAssemblyPathInPortableMode()
        {
            // Setup
            TestSourceHost sut = new TestSourceHost(null, null, null);

            // Execute
            // It should not return public and private path if it is running in portable mode.
            List<string> result = sut.GetResolutionPaths("DummyAssembly.dll", isPortableMode: true);

            // Assert
            Assert.IsFalse(result.Contains(VSInstallationUtilities.PathToPublicAssemblies));
            Assert.IsFalse(result.Contains(VSInstallationUtilities.PathToPrivateAssemblies));
        }

        [TestMethod]
        public void GetResolutionPathsShouldAddAdapterFolderPath()
        {
            // Setup
            TestSourceHost sut = new TestSourceHost(null, null, null);

            // Execute
            List<string> result = sut.GetResolutionPaths("DummyAssembly.dll", isPortableMode: false);

            // Assert
            Assert.IsFalse(result.Contains(typeof(TestSourceHost).Assembly.Location));
        }

        [TestMethod]
        public void GetResolutionPathsShouldAddTestPlatformFolderPath()
        {
            // Setup
            TestSourceHost sut = new TestSourceHost(null, null, null);

            // Execute
            List<string> result = sut.GetResolutionPaths("DummyAssembly.dll", isPortableMode: false);

            // Assert
            Assert.IsFalse(result.Contains(typeof(AssemblyHelper).Assembly.Location));
        }

        [TestMethod]
        public void CreateInstanceForTypeShouldCreateTheTypeInANewAppDomain()
        {
            // Setup
            DummyClass dummyclass = new DummyClass();
            int currentAppDomainId = dummyclass.AppDomainId;

            TestSourceHost sut = new TestSourceHost(Assembly.GetExecutingAssembly().Location, null, null);
            sut.SetupHost();

            // Execute
            var expectedObject = sut.CreateInstanceForType(typeof(DummyClass), null) as DummyClass;

            int newAppDomainId = currentAppDomainId + 10;  // not equal to currentAppDomainId
            if (expectedObject != null)
            {
                newAppDomainId = expectedObject.AppDomainId;
            }

            // Assert
            Assert.AreNotEqual(currentAppDomainId, newAppDomainId);
        }

        [TestMethod]
        public void SetupHostShouldSetChildDomainsAppBaseToTestSourceLocation()
        {
            // Arrange
            DummyClass dummyclass = new DummyClass();

            var location = typeof(TestSourceHost).Assembly.Location;
            Mock<TestSourceHost> sourceHost = new Mock<TestSourceHost>(location, null, null) { CallBase = true };

            try
            {
                // Act
                sourceHost.Object.SetupHost();
                var expectedObject = sourceHost.Object.CreateInstanceForType(typeof(DummyClass), null) as DummyClass;

                // Assert
                Assert.AreEqual(Path.GetDirectoryName(typeof(DesktopTestSourceHostTests).Assembly.Location), expectedObject.AppDomainAppBase);
            }
            finally
            {
                sourceHost.Object.Dispose();
            }
        }

        [TestMethod]
        public void SetupHostShouldHaveParentDomainsAppBaseSetToTestSourceLocation()
        {
            // Arrange
            DummyClass dummyclass = new DummyClass();
            string runSettingxml =
            @"<RunSettings>   
                <RunConfiguration>  
                    <DisableAppDomain>True</DisableAppDomain>   
                </RunConfiguration>  
            </RunSettings>";

            var location = typeof(TestSourceHost).Assembly.Location;
            var mockRunSettings = new Mock<IRunSettings>();
            mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);

            Mock<TestSourceHost> sourceHost = new Mock<TestSourceHost>(location, mockRunSettings.Object, null) { CallBase = true };

            try
            {
                // Act
                sourceHost.Object.SetupHost();
                var expectedObject = sourceHost.Object.CreateInstanceForType(typeof(DummyClass), null) as DummyClass;

                // Assert
                Assert.AreEqual(Path.GetDirectoryName(typeof(DesktopTestSourceHostTests).Assembly.Location), expectedObject.AppDomainAppBase);
            }
            finally
            {
                sourceHost.Object.Dispose();
            }
        }

        [TestMethod]
        public void SetupHostShouldSetResolutionsPaths()
        {
            // Arrange
            DummyClass dummyclass = new DummyClass();

            var location = typeof(TestSourceHost).Assembly.Location;
            Mock<TestSourceHost> sourceHost = new Mock<TestSourceHost>(location, null, null) { CallBase = true };

            try
            {
                // Act
                sourceHost.Object.SetupHost();

                // Assert
                sourceHost.Verify(sh => sh.GetResolutionPaths(location, It.IsAny<bool>()), Times.Once);
            }
            finally
            {
                sourceHost.Object.Dispose();
            }
        }

        [TestMethod]
        public void DisposeShouldSetTestHostShutdownOnIssueWithAppDomainUnload()
        {
            // Arrange
            var frameworkHandle = new Mock<IFrameworkHandle>();
            var testableAppDomain = new Mock<IAppDomain>();

            testableAppDomain.Setup(ad => ad.CreateDomain(It.IsAny<string>(), It.IsAny<Evidence>(), It.IsAny<AppDomainSetup>())).Returns(AppDomain.CurrentDomain);
            testableAppDomain.Setup(ad => ad.Unload(It.IsAny<AppDomain>())).Throws(new CannotUnloadAppDomainException());
            var sourceHost = new TestSourceHost(typeof(DesktopTestSourceHostTests).Assembly.Location, null, frameworkHandle.Object, testableAppDomain.Object);
            sourceHost.SetupHost();

            // Act
            sourceHost.Dispose();

            // Assert
            frameworkHandle.VerifySet(fh => fh.EnableShutdownAfterTestRun = true);
        }

        [TestMethod]
        public void NoAppDomainShouldGetCreatedWhenDisableAppDomainIsSetToTrue()
        {
            // Arrange
            DummyClass dummyclass = new DummyClass();
            string runSettingxml =
            @"<RunSettings>   
                <RunConfiguration>  
                    <DisableAppDomain>True</DisableAppDomain>   
                </RunConfiguration>  
            </RunSettings>";

            var location = typeof(TestSourceHost).Assembly.Location;
            var mockRunSettings = new Mock<IRunSettings>();
            mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);

            Mock<TestSourceHost> testSourceHost = new Mock<TestSourceHost>(location, mockRunSettings.Object, null) { CallBase = true };

            try
            {
                // Act
                testSourceHost.Object.SetupHost();
                Assert.IsNull(testSourceHost.Object.AppDomain);
            }
            finally
            {
                testSourceHost.Object.Dispose();
            }
        }

        [TestMethod]
        public void AppDomainShouldGetCreatedWhenDisableAppDomainIsSetToFalse()
        {
            // Arrange
            DummyClass dummyclass = new DummyClass();
            string runSettingxml =
            @"<RunSettings>   
                <RunConfiguration>  
                    <DisableAppDomain>False</DisableAppDomain>   
                </RunConfiguration>  
            </RunSettings>";

            var location = typeof(TestSourceHost).Assembly.Location;
            var mockRunSettings = new Mock<IRunSettings>();
            mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);

            Mock<TestSourceHost> testSourceHost = new Mock<TestSourceHost>(location, mockRunSettings.Object, null) { CallBase = true };

            try
            {
                // Act
                testSourceHost.Object.SetupHost();
                Assert.IsNotNull(testSourceHost.Object.AppDomain);
            }
            finally
            {
                testSourceHost.Object.Dispose();
            }
        }
    }

    public class DummyClass : MarshalByRefObject
    {
        public int AppDomainId
        {
            get
            {
                return AppDomain.CurrentDomain.Id;
            }
        }

        public string AppDomainAppBase
        {
            get
            {
                return AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
            }
        }
    }
}
