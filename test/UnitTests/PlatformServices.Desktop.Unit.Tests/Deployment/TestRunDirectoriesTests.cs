// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.PlatformServices.Desktop.UnitTests.Deployment
{
    extern alias FrameworkV1;

    using System;
    using System.IO;

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;

    using Assert = FrameworkV1.Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using TestClass = FrameworkV1.Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestMethod = FrameworkV1.Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;

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
