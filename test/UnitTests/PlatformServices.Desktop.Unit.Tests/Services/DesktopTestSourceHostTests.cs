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
            Assert.AreEqual(result.Contains(VSInstallationUtilities.PathToPublicAssemblies), true);
            Assert.AreEqual(result.Contains(VSInstallationUtilities.PathToPrivateAssemblies), true);
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
            Assert.AreEqual(result.Contains(VSInstallationUtilities.PathToPublicAssemblies), false);
            Assert.AreEqual(result.Contains(VSInstallationUtilities.PathToPrivateAssemblies), false);
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

        /// <summary>
        /// This test should ideally be choosing a different path for the test source. Currently both the test source and the adapter
        /// are in the same location. However when we move to run these tests with the V2 itself, then this would be valid.
        /// Leaving the test running till then.
        /// </summary>
        [TestMethod]
        public void SetupHostShouldSetNewDomainsAppBaseToTestSourceLocationForFullCLRTests()
        {
            // Arrange
            DummyClass dummyclass = new DummyClass();

            var location = typeof(DesktopTestSourceHostTests).Assembly.Location;
            var sourceHost = new Mock<TestSourceHost>(location, null, null) { CallBase = true };
            try
            {
                sourceHost.Setup(tsh => tsh.GetTargetFrameworkVersionString(location))
                    .Returns(".NETFramework,Version=v4.5.1");

                // Act
                sourceHost.Object.SetupHost();
                var expectedObject =
                    sourceHost.Object.CreateInstanceForType(typeof(DummyClass), null) as DummyClass;

                // Assert
                Assert.AreEqual(Path.GetDirectoryName(location), expectedObject.AppDomainAppBase);
            }
            finally
            {
                sourceHost.Object.Dispose();
            }
        }

        [TestMethod]
        public void SetupHostShouldSetNewDomainsAppBaseToAdaptersLocationForNonFullCLRTests()
        {
            // Arrange
            DummyClass dummyclass = new DummyClass();

            var location = typeof(TestSourceHost).Assembly.Location;
            Mock<TestSourceHost> sourceHost = new Mock<TestSourceHost>(location, null, null) { CallBase = true };

            try
            {
                sourceHost.Setup(tsh => tsh.GetTargetFrameworkVersionString(location)).Returns(".NETCore,Version=v5.0");

                // Act
                sourceHost.Object.SetupHost();
                var expectedObject = sourceHost.Object.CreateInstanceForType(typeof(DummyClass), null) as DummyClass;

                // Assert
                Assert.AreEqual(Path.GetDirectoryName(location), expectedObject.AppDomainAppBase);
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
