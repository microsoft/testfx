// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.PlatformServices.Tests.Deployment
{
#if NETCOREAPP1_1
    using Microsoft.VisualStudio.TestTools.UnitTesting;
#else
    extern alias FrameworkV1;

    using Assert = FrameworkV1.Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using TestClass = FrameworkV1.Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestMethod = FrameworkV1.Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
#endif
    using System;
    using System.IO;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;

    [TestClass]
    public class TestRunDirectoriesTests
    {
        private TestRunDirectories testRunDirectories = new TestRunDirectories(@"C:\temp");

        [TestMethod]
        public void InMachineNameDirectoryShouldReturnMachineSpecificDeploymentDirectory()
        {
            Assert.AreEqual(
                Path.Combine(@"C:\temp\In", Environment.MachineName),
                this.testRunDirectories.InMachineNameDirectory);
        }
    }
}
