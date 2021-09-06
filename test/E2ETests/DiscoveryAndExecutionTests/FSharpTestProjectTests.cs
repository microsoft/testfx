// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.MSTestV2.Smoke.DiscoveryAndExecutionTests
{
    using Microsoft.MSTestV2.CLIAutomation;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using System.IO;

    [TestClass]
    public class FSharpTestProjectTests : CLITestBase
    {
        private const string TestAssembly = "FSharpTestProject.dll";

        [TestMethod]
        public void ExecuteCsvTestDataSourceTests()
        {
            // Arrange
            var assemblyPath = Path.IsPathRooted(TestAssembly) ? TestAssembly : this.GetAssetFullPath(TestAssembly);

            // Act
            var testCases = DiscoverTests(assemblyPath);
            var testResults = RunTests(assemblyPath, testCases);

            // Assert
            Assert.That.TestsPassed(testResults, "Test method passing with a . in it");
            Assert.That.PassedTestCount(testResults, 1);
            Assert.That.FailedTestCount(testResults, 0);
        }
    }
}
