// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.MSTestV2.Smoke.DiscoveryAndExecutionTests
{
    using System.IO;
    using Microsoft.MSTestV2.CLIAutomation;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class DiscoverInternalTestClassesTests : CLITestBase
    {
        private const string TestAssembly = "DiscoverInternalTestClassesProject.dll";

        [TestMethod]
        public void InternalTestClassesAreDiscoveredWhenTheDiscoverInternalTestClassesAttributeIsPresent()
        {
            // Arrange
            var assemblyPath = Path.IsPathRooted(TestAssembly) ? TestAssembly : this.GetAssetFullPath(TestAssembly);

            // Act
            var testCases = DiscoverTests(assemblyPath);
            var testResults = RunTests(assemblyPath, testCases);

            // Assert
            Assert.That.TestsPassed(
                testResults,
                "TopLevelInternalClass_TestMethod1",
                "NestedInternalClass_TestMethod1"
            );
        }
    }
}
