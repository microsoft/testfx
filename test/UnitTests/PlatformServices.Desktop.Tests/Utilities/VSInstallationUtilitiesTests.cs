// Copyright (c) Microsoft. All rights reserved.

extern alias FrameworkV2;
using FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting;

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

    [FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute]
    public class VSInstallationUtilitiesTests
    {
        [FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute]
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

        [FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute]
        public void CheckResolutionPathsDoNotContainPrivateAssembliesPathTest()
        {
            TestSourceHost isolatedHost = new TestSourceHost(null, null);
            List<string> paths = isolatedHost.GetResolutionPaths(Assembly.GetExecutingAssembly().FullName, true);
            Assert.IsFalse(paths.Contains(Constants.PublicAssemblies) || paths.Contains(Constants.PrivateAssemblies));
        }
    }
}
