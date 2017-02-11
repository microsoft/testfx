// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
    public class VSInstallationUtilitiesTests
    {
        [TestMethod]
        public void CheckPortableModeRunningTest()
        {
            string path = Environment.GetEnvironmentVariable(Constants.PortableVsTestLocation);
            if (string.IsNullOrEmpty(path))
            {
                Assert.Inconclusive("This test required Portable vstest to be installed");
            }
            else
            {
                Assert.IsTrue(VSInstallationUtilities.IsProcessRunningInPortableMode(path));
            }
        }

        [TestMethod]
        public void CheckResolutionPathsDoNotContainPrivateAssembliesPathTest()
        {
            TestSourceHost isolatedHost = new TestSourceHost(null, null);
            List<string> paths = isolatedHost.GetResolutionPaths(Assembly.GetExecutingAssembly().FullName, true);
            Assert.IsFalse(paths.Contains(Constants.PublicAssemblies) || paths.Contains(Constants.PrivateAssemblies));
        }
    }
}
