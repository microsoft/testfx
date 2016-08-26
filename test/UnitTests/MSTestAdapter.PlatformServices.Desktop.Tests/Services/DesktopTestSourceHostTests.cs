// Copyright (c) Microsoft. All rights reserved.


namespace MSTestAdapter.PlatformServices.Desktop.UnitTests
{
    extern alias FrameworkV1;
    extern alias FrameworkV2;

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;
    using System;
    using System.Collections.Generic;
    using System.Reflection;

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
            TestSourceHost sut = new TestSourceHost();

            // Execute
            // It should return public and private path if it is not running in portable mode.
            List<string> result = sut.GetResolutionPaths("DummyAssembly.dll", isPortableMode: false);

            // Assert
            Assert.AreEqual(result.Contains(VSInstallationUtilities.PathToPublicAssemblies), true);
            Assert.AreEqual(result.Contains(VSInstallationUtilities.PathToPrivateAssemblies), true);
        }

        [TestMethod]
        public void GetResolutionPathsShouldNotAddPublicAndPrivateAssemblyPath()
        {
            // Setup
            TestSourceHost sut = new TestSourceHost();

            // Execute
            // It should not return public and private path if it is running in portable mode.
            List<string> result = sut.GetResolutionPaths("DummyAssembly.dll", isPortableMode: true);

            // Assert
            Assert.AreEqual(result.Contains(VSInstallationUtilities.PathToPublicAssemblies), false);
            Assert.AreEqual(result.Contains(VSInstallationUtilities.PathToPrivateAssemblies), false);
        }

        [TestMethod]
        public void CreateInstanceForTypeTest()
        {
            // Setup
            DummyClass dummyclass = new DummyClass();
            int currentAppDomainId = dummyclass.AppDomainId();

            TestSourceHost sut = new TestSourceHost();

            // Execute
            var expectedObject = sut.CreateInstanceForType(typeof(DummyClass), null, Assembly.GetExecutingAssembly().Location, null) as DummyClass;

            int newAppDomainId = currentAppDomainId + 10;  // not equal to currentAppDomainId
            if (expectedObject != null)
            {
                newAppDomainId = expectedObject.AppDomainId();
            }
            //Assert
            Assert.AreNotEqual(currentAppDomainId, newAppDomainId);
        }
    }

    public class DummyClass : MarshalByRefObject
    {
        public int AppDomainId()
        {
            return AppDomain.CurrentDomain.Id;
        }
    }
}
