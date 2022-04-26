// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.MSTestV2.Smoke.DiscoveryAndExecutionTests
{
    using Microsoft.MSTestV2.CLIAutomation;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using System.IO;
    using System.Linq;

    [TestClass]
    public class TestDataSourceExtensibilityTests : CLITestBase
    {
        private const string TestAssembly = "FxExtensibilityTestProject.dll";

        /*
            Add tests for:
             - Ignored tests are discovered during discovery
             - Ignored tests are not expanded (DataRow, DataSource, etc)
         */

        [TestMethod]
        public void CustomTestDataSourceTests()
        {
            // Arrange
            var assemblyPath = Path.IsPathRooted(TestAssembly) ? TestAssembly : this.GetAssetFullPath(TestAssembly);

            // Act
            var testCases = DiscoverTests(assemblyPath, "CustomTestDataSourceTestMethod1");
            var testResults = RunTests(assemblyPath, testCases);

            // Assert
            Assert.That.ContainsTestsPassed(testResults, "CustomTestDataSourceTestMethod1 (1,2,3)", "CustomTestDataSourceTestMethod1 (4,5,6)");
        }

        [TestMethod]
        public void AssertExtensibilityTests()
        {
            // Arrange
            var assemblyPath = Path.IsPathRooted(TestAssembly) ? TestAssembly : this.GetAssetFullPath(TestAssembly);

            // Act
            var testCases = DiscoverTests(assemblyPath, "FxExtensibilityTestProject.AssertExTest");
            var testResults = RunTests(assemblyPath, testCases);

            // Assert
            Assert.That.ContainsTestsPassed(testResults, "BasicAssertExtensionTest", "ChainedAssertExtensionTest");
            Assert.That.ContainsTestsFailed(testResults, "BasicFailingAssertExtensionTest", "ChainedFailingAssertExtensionTest");
        }

        [TestMethod]
        public void ExecuteCustomTestExtensibilityTests()
        {
            // Arrange
            var assemblyPath = Path.IsPathRooted(TestAssembly) ? TestAssembly : this.GetAssetFullPath(TestAssembly);

            // Act
            var testCases = DiscoverTests(assemblyPath, "(Name~CustomTestMethod1)|(Name~CustomTestClass1)");
            var testResults = RunTests(assemblyPath, testCases);

            // Assert
            Assert.That.ContainsTestsPassed(testResults,
                "CustomTestMethod1 - Execution number 1",
                "CustomTestMethod1 - Execution number 2",
                "CustomTestMethod1 - Execution number 4",
                "CustomTestMethod1 - Execution number 5",
                "CustomTestClass1 - Execution number 1",
                "CustomTestClass1 - Execution number 2",
                "CustomTestClass1 - Execution number 4",
                "CustomTestClass1 - Execution number 5"
            );

            Assert.That.ContainsTestsFailed(testResults,
                "CustomTestMethod1 - Execution number 3",
                "CustomTestClass1 - Execution number 3"
            );
        }

        [TestMethod]
        public void ExecuteCustomTestExtensibilityWithTestDataTests()
        {
            // Arrange
            var assemblyPath = Path.IsPathRooted(TestAssembly) ? TestAssembly : this.GetAssetFullPath(TestAssembly);

            // Act
            var testCases = DiscoverTests(assemblyPath, "Name~CustomTestMethod2");
            var testResults = RunTests(assemblyPath, testCases);

            // Assert
            Assert.That.TestsPassed(testResults,
                "CustomTestMethod2 (B)",
                "CustomTestMethod2 (B)",
                "CustomTestMethod2 (B)"
            );

            Assert.That.TestsFailed(testResults,
                "CustomTestMethod2 (A)",
                "CustomTestMethod2 (A)",
                "CustomTestMethod2 (A)",
                "CustomTestMethod2 (C)",
                "CustomTestMethod2 (C)",
                "CustomTestMethod2 (C)"
            );
        }

        [TestMethod]
        public void BailOutWhenDuplicateTestDisplayName()
        {
            // Arrange
            var assemblyPath = Path.IsPathRooted(TestAssembly) ? TestAssembly : this.GetAssetFullPath(TestAssembly);

            // Act
            var testCases = DiscoverTests(assemblyPath, "Name~DynamicDataDiscoveryBailOutTestMethod1");
            var testResults = RunTests(assemblyPath, testCases);

            // Assert
            Assert.That.TestsDiscovered(testCases, "FxExtensibilityTestProject.DynamicDataDiscoveryBailOutTests.DynamicDataDiscoveryBailOutTestMethod1");
            Assert.That.PassedTestCount(testResults, 3);
        }
    }
}
