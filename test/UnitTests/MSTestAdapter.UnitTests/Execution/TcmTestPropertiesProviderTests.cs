// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

using TestFramework.ForTestingMSTest;

using TestAdapterConstants = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Constants;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public class TcmTestPropertiesProviderTests : TestContainer
{
    private readonly TestProperty[] _tcmKnownProperties =
    [
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
        TestAdapterConstants.TestPointIdProperty,
    ];

    public void GetTcmPropertiesShouldReturnEmptyDictionaryIfTestCaseIsNull()
    {
        var tcmProperties = TcmTestPropertiesProvider.GetTcmProperties(null);
        Verify(tcmProperties.Count == 0);
    }

    public void GetTcmPropertiesShouldReturnEmptyDictionaryIfTestCaseIdIsZero()
    {
        var testCase = new TestCase("PassingTestFomTestCase", new Uri("http://sampleUri/"), "unittestproject1.dll");
        var propertiesValue = new object[]
        {
            32, 534, 5, "sample build directory", "sample build flavor",
            "132456", "sample build platform", "http://sampleBuildUri/",
            "http://samplecollectionuri/", "sample team project", false,
            0, 54, "sample configuration name", 345,
        };
        SetTestCaseProperties(testCase, propertiesValue);

        var tcmProperties = TcmTestPropertiesProvider.GetTcmProperties(testCase);
        Verify(tcmProperties.Count == 0);
    }

    public void GetTcmPropertiesShouldGetAllPropertiesFromTestCase()
    {
        var testCase = new TestCase("PassingTestFomTestCase", new Uri("http://sampleUri/"), "unittestproject1.dll");
        var propertiesValue = new object[]
        {
            32, 534, 5, "sample build directory", "sample build flavor",
            "132456", "sample build platform", "http://sampleBuildUri/",
            "http://samplecollectionuri/", "sample team project", false,
            1401, 54, "sample configuration name", 345,
        };
        SetTestCaseProperties(testCase, propertiesValue);

        var tcmProperties = TcmTestPropertiesProvider.GetTcmProperties(testCase);

        VerifyTcmProperties(tcmProperties, testCase);
    }

    public void GetTcmPropertiesShouldCopyMultiplePropertiesCorrectlyFromTestCase()
    {
        // Verify 1st call.
        var testCase1 = new TestCase("PassingTestFomTestCase", new Uri("http://sampleUri/"), "unittestproject1.dll");
        var propertiesValue1 = new object[]
        {
            32, 534, 5, "sample build directory", "sample build flavor",
            "132456", "sample build platform", "http://sampleBuildUri/",
            "http://samplecollectionuri/", "sample team project", false,
            1401, 54, "sample configuration name", 345,
        };
        SetTestCaseProperties(testCase1, propertiesValue1);
        var tcmProperties1 = TcmTestPropertiesProvider.GetTcmProperties(testCase1);
        VerifyTcmProperties(tcmProperties1, testCase1);

        // Verify 2nd call.
        var testCase2 = new TestCase("PassingTestFomTestCase2", new Uri("http://sampleUri2/"), "unittestproject2.dll");
        var propertiesValue2 = new object[]
        {
            33, 535, 6, "sample build directory 2", "sample build flavor 2",
            "132457", "sample build platform 2", "http://sampleBuildUri2/",
            "http://samplecollectionuri2/", "sample team project", true,
            1403, 55, "sample configuration name 2", 346,
        };
        SetTestCaseProperties(testCase2, propertiesValue2);
        var tcmProperties2 = TcmTestPropertiesProvider.GetTcmProperties(testCase2);
        VerifyTcmProperties(tcmProperties2, testCase2);
    }

    public void GetTcmPropertiesShouldHandleDuplicateTestsProperlyFromTestCase()
    {
        // Verify 1st call.
        var testCase1 = new TestCase("PassingTestFomTestCase", new Uri("http://sampleUri/"), "unittestproject1.dll");
        var propertiesValue1 = new object[]
        {
            32, 534, 5, "sample build directory", "sample build flavor",
            "132456", "sample build platform", "http://sampleBuildUri/",
            "http://samplecollectionuri/", "sample team project", false,
            1401, 54, "sample configuration name", 345,
        };
        SetTestCaseProperties(testCase1, propertiesValue1);
        var tcmProperties1 = TcmTestPropertiesProvider.GetTcmProperties(testCase1);
        VerifyTcmProperties(tcmProperties1, testCase1);

        // Verify 2nd call.
        var testCase2 = new TestCase("PassingTestFomTestCase", new Uri("http://sampleUri/"), "unittestproject1.dll");
        var propertiesValue2 = new object[]
        {
            33, 535, 6, "sample build directory 2", "sample build flavor 2",
            "132457", "sample build platform 2", "http://sampleBuildUri2/",
            "http://samplecollectionuri2/", "sample team project", true,
            1403, 55, "sample configuration name 2", 346,
        };
        SetTestCaseProperties(testCase2, propertiesValue2);
        var tcmProperties2 = TcmTestPropertiesProvider.GetTcmProperties(testCase2);
        VerifyTcmProperties(tcmProperties2, testCase2);

        // Verify 3rd call.
        var testCase3 = new TestCase("PassingTestFomTestCase2", new Uri("http://sampleUri/"), "unittestproject2.dll");
        var propertiesValue3 = new object[]
        {
            34, 536, 7, "sample build directory 3", "sample build flavor 3",
            "132458", "sample build platform 3", "http://sampleBuildUri3/",
            "http://samplecollectionuri3/", "sample team project2", true,
            1404, 55, "sample configuration name 3", 347,
        };
        SetTestCaseProperties(testCase3, propertiesValue3);
        var tcmProperties3 = TcmTestPropertiesProvider.GetTcmProperties(testCase3);
        VerifyTcmProperties(tcmProperties3, testCase3);
    }

    private void SetTestCaseProperties(TestCase testCase, object[] propertiesValue)
    {
        var tcmKnownPropertiesEnumerator = _tcmKnownProperties.GetEnumerator();

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
        foreach (var property in _tcmKnownProperties)
        {
            Verify(testCase.GetPropertyValue(property).Equals(tcmProperties[property]));
        }
    }
}
