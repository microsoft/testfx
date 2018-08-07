// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution
{
    extern alias FrameworkV1;
    extern alias FrameworkV2;
    extern alias FrameworkV2CoreExtension;

    using System;
    using System.Collections.Generic;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;

    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using TestAdapterConstants = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Constants;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;

    [TestClass]
    public class TcmTestPropertiesProviderTests
    {
        private TestProperty[] tcmKnownProperties = new TestProperty[]
        {
            TestAdapterConstants.TestRunIdProperty,
            TestAdapterConstants.TestPlanIdProperty,
            TestAdapterConstants.BuildConfigurationIdProperty,
            TestAdapterConstants.BuildDirectoryProperty,
            TestAdapterConstants.BuildFlavorProperty,
            TestAdapterConstants.BuildNumberProperty,
            TestAdapterConstants.BuildPlatformProperty,
            TestAdapterConstants.BuildUriProperty,
            TestAdapterConstants.TfsServerCollectionUrlProperty,
            TestAdapterConstants.TfsTeamProjectProperty,
            TestAdapterConstants.IsInLabEnvironmentProperty,
            TestAdapterConstants.TestCaseIdProperty,
            TestAdapterConstants.TestConfigurationIdProperty,
            TestAdapterConstants.TestConfigurationNameProperty,
            TestAdapterConstants.TestPointIdProperty
        };

        [TestMethod]
        public void GetTcmPropertiesShouldReturnEmptyDictionaryIfTestCaseIsNull()
        {
            var tcmProperties = TcmTestPropertiesProvider.GetTcmProperties(null);
            Assert.AreEqual(0, tcmProperties.Count);
        }

        [TestMethod]
        public void GetTcmPropertiesShouldReturnEmptyDictionaryIfTestCaseIdIsZero()
        {
            var testCase = new TestCase("PassingTestFomTestCase", new Uri("http://sampleUri/"), "unittestproject1.dll");
            var propertiesValue = new object[]
            {
                32, 534, 5, "sample build directory", "sample build flavor",
                "132456", "sample build platform", "http://sampleBuildUri/",
                "http://samplecollectionuri/", "sample team project", false,
                0, 54, "sample configuration name", 345
            };
            this.SetTestCaseProperties(testCase, propertiesValue);

            var tcmProperties = TcmTestPropertiesProvider.GetTcmProperties(testCase);
            Assert.AreEqual(0, tcmProperties.Count);
        }

        [TestMethod]
        public void GetTcmPropertiesShouldGetAllPropertiesFromTestCase()
        {
            var testCase = new TestCase("PassingTestFomTestCase", new Uri("http://sampleUri/"), "unittestproject1.dll");
            var propertiesValue = new object[]
            {
                32, 534, 5, "sample build directory", "sample build flavor",
                "132456", "sample build platform", "http://sampleBuildUri/",
                "http://samplecollectionuri/", "sample team project", false,
                1401, 54, "sample configuration name", 345
            };
            this.SetTestCaseProperties(testCase, propertiesValue);

            var tcmProperties = TcmTestPropertiesProvider.GetTcmProperties(testCase);

            this.VerifyTcmProperties(tcmProperties, testCase);
        }

        [TestMethod]
        public void GetTcmPropertiesShouldCopyMultiplePropertiesCorrectlyFromTestCase()
        {
            // Verify 1st call.
            var testCase1 = new TestCase("PassingTestFomTestCase", new Uri("http://sampleUri/"), "unittestproject1.dll");
            var propertiesValue1 = new object[]
            {
                32, 534, 5, "sample build directory", "sample build flavor",
                "132456", "sample build platform", "http://sampleBuildUri/",
                "http://samplecollectionuri/", "sample team project", false,
                1401, 54, "sample configuration name", 345
            };
            this.SetTestCaseProperties(testCase1, propertiesValue1);
            var tcmProperties1 = TcmTestPropertiesProvider.GetTcmProperties(testCase1);
            this.VerifyTcmProperties(tcmProperties1, testCase1);

            // Verify 2nd call.
            var testCase2 = new TestCase("PassingTestFomTestCase2", new Uri("http://sampleUri2/"), "unittestproject2.dll");
            var propertiesValue2 = new object[]
            {
                33, 535, 6, "sample build directory 2", "sample build flavor 2",
                "132457", "sample build platform 2", "http://sampleBuildUri2/",
                "http://samplecollectionuri2/", "sample team project", true,
                1403, 55, "sample configuration name 2", 346
            };
            this.SetTestCaseProperties(testCase2, propertiesValue2);
            var tcmProperties2 = TcmTestPropertiesProvider.GetTcmProperties(testCase2);
            this.VerifyTcmProperties(tcmProperties2, testCase2);
        }

        [TestMethod]
        public void GetTcmPropertiesShouldHandleDuplicateTestsProperlyFromTestCase()
        {
            // Verify 1st call.
            var testCase1 = new TestCase("PassingTestFomTestCase", new Uri("http://sampleUri/"), "unittestproject1.dll");
            var propertiesValue1 = new object[]
            {
                32, 534, 5, "sample build directory", "sample build flavor",
                "132456", "sample build platform", "http://sampleBuildUri/",
                "http://samplecollectionuri/", "sample team project", false,
                1401, 54, "sample configuration name", 345
            };
            this.SetTestCaseProperties(testCase1, propertiesValue1);
            var tcmProperties1 = TcmTestPropertiesProvider.GetTcmProperties(testCase1);
            this.VerifyTcmProperties(tcmProperties1, testCase1);

            // Verify 2nd call.
            var testCase2 = new TestCase("PassingTestFomTestCase", new Uri("http://sampleUri/"), "unittestproject1.dll");
            var propertiesValue2 = new object[]
            {
                33, 535, 6, "sample build directory 2", "sample build flavor 2",
                "132457", "sample build platform 2", "http://sampleBuildUri2/",
                "http://samplecollectionuri2/", "sample team project", true,
                1403, 55, "sample configuration name 2", 346
            };
            this.SetTestCaseProperties(testCase2, propertiesValue2);
            var tcmProperties2 = TcmTestPropertiesProvider.GetTcmProperties(testCase2);
            this.VerifyTcmProperties(tcmProperties2, testCase2);

            // Verify 3rd call.
            var testCase3 = new TestCase("PassingTestFomTestCase2", new Uri("http://sampleUri/"), "unittestproject2.dll");
            var propertiesValue3 = new object[]
            {
                34, 536, 7, "sample build directory 3", "sample build flavor 3",
                "132458", "sample build platform 3", "http://sampleBuildUri3/",
                "http://samplecollectionuri3/", "sample team project2", true,
                1404, 55, "sample configuration name 3", 347
            };
            this.SetTestCaseProperties(testCase3, propertiesValue3);
            var tcmProperties3 = TcmTestPropertiesProvider.GetTcmProperties(testCase3);
            this.VerifyTcmProperties(tcmProperties3, testCase3);
        }

        private void SetTestCaseProperties(TestCase testCase, object[] propertiesValue)
        {
            var tcmKnownPropertiesEnumerator = this.tcmKnownProperties.GetEnumerator();

            var propertiesValueEnumerator = propertiesValue.GetEnumerator();
            while (tcmKnownPropertiesEnumerator.MoveNext() && propertiesValueEnumerator.MoveNext())
            {
                var property = tcmKnownPropertiesEnumerator.Current;
                var value = propertiesValueEnumerator.Current;
                testCase.SetPropertyValue(property as TestProperty, value);
            }
        }

        private void VerifyTcmProperties(IDictionary<TestProperty, object> tcmProperties, TestCase testCase)
        {
            foreach (var property in this.tcmKnownProperties)
            {
                Assert.AreEqual(testCase.GetPropertyValue(property), tcmProperties[property]);
            }
        }
    }
}
