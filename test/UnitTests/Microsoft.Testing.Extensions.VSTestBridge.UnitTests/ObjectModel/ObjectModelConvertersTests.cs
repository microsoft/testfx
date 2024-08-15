// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Extensions.VSTestBridge.ObjectModel;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Microsoft.Testing.Extensions.VSTestBridge.UnitTests.ObjectModel;

[TestGroup]
public sealed class ObjectModelConvertersTests : TestBase
{
    private static readonly IClientInfo TestClient = new ClientInfoService("UnitTest", string.Empty);
    private static readonly IClientInfo VSTestClient = new ClientInfoService(WellKnownClients.VisualStudio, string.Empty);

    public ObjectModelConvertersTests(ITestExecutionContext testExecutionContext)
        : base(testExecutionContext)
    {
    }

    public void ToTestNode_WhenTestCaseHasDisplayName_TestNodeDisplayNameUsesIt()
    {
        TestCase testCase = new("SomeFqn", new("executor://uri", UriKind.Absolute), "source.cs")
        {
            DisplayName = "MyDisplayName",
        };
        var testNode = testCase.ToTestNode(false, TestClient);

        Assert.AreEqual("MyDisplayName", testNode.DisplayName);
    }

    public void ToTestNode_WhenTestCaseHasNoDisplayName_TestNodeDisplayNameUsesIt()
    {
        TestCase testCase = new("SomeFqn", new("executor://uri", UriKind.Absolute), "source.cs");
        var testNode = testCase.ToTestNode(false, TestClient);

        Assert.AreEqual("SomeFqn", testNode.DisplayName);
    }

    public void ToTestNode_WhenTestResultHasCodeFilePath_SetsTestFileLocationProperty()
    {
        TestResult testResult = new(new("SomeFqn", new("executor://uri", UriKind.Absolute), "source.cs")
        {
            CodeFilePath = "FilePath",
        });
        var testNode = testResult.ToTestNode(false, TestClient);
        Assert.AreEqual("FilePath", testNode.Properties.Single<TestFileLocationProperty>().FilePath);
    }

    public void ToTestNode_WhenTestResultOutcomeIsFailed_TestNodePropertiesContainFailedTestNodeStateProperty()
    {
        TestResult testResult = new(new TestCase("SomeFqn", new("executor://uri", UriKind.Absolute), "source.cs"))
        {
            Outcome = TestOutcome.Failed,
            ErrorMessage = "SomeErrorMessage",
            ErrorStackTrace = "SomeStackTrace",
        };
        var testNode = testResult.ToTestNode(false, TestClient);

        FailedTestNodeStateProperty[] failedTestNodeStateProperties = testNode.Properties.OfType<FailedTestNodeStateProperty>().ToArray();
        Assert.IsTrue(failedTestNodeStateProperties.Length == 1);
        Assert.IsTrue(failedTestNodeStateProperties[0].Exception is VSTestException);
        Assert.AreEqual(testResult.ErrorStackTrace, failedTestNodeStateProperties[0].Exception!.StackTrace);
        Assert.AreEqual(testResult.ErrorMessage, failedTestNodeStateProperties[0].Exception!.Message);
    }

    public void ToTestNode_WhenTestResultHasMSTestDiscovererTestCategoryTestProperty_TestNodePropertiesContainTheCategoryInTraits()
    {
        TestResult testResult = new(new TestCase("SomeFqn", new("executor://uri", UriKind.Absolute), "source.cs"));
        var testCategoryProperty = TestProperty.Register("MSTestDiscoverer.TestCategory", "Label", typeof(string[]), TestPropertyAttributes.None, typeof(TestCase));
        testResult.SetPropertyValue<string[]>(testCategoryProperty, ["category1"]);

        var testNode = testResult.ToTestNode(false, VSTestClient);

        SerializableNamedKeyValuePairsStringProperty[] errorTestNodeStateProperties = testNode.Properties.OfType<SerializableNamedKeyValuePairsStringProperty>().ToArray();
        Assert.IsTrue(errorTestNodeStateProperties.Length == 1);
        Assert.IsTrue(errorTestNodeStateProperties[0].Name == "traits");
        Assert.IsTrue(errorTestNodeStateProperties[0].Pairs.Length == 1);
        Assert.IsTrue(errorTestNodeStateProperties[0].Pairs[0].Key == "category1");
    }

    public void ToTestNode_WhenTestResultHasMSTestDiscovererTestCategoryTestPropertyWithTrxEnabled_TestNodePropertiesContainTrxCategoriesProperty()
    {
        TestResult testResult = new(new TestCase("assembly.class.SomeFqn", new("executor://uri", UriKind.Absolute), "source.cs"));
        var testCategoryProperty = TestProperty.Register("MSTestDiscoverer.TestCategory", "Label", typeof(string[]), TestPropertyAttributes.None, typeof(TestCase));
        testResult.SetPropertyValue<string[]>(testCategoryProperty, ["category1"]);

        var testNode = testResult.ToTestNode(true, VSTestClient);

        TrxCategoriesProperty[] trxCategoriesProperty = testNode.Properties.OfType<TrxCategoriesProperty>().ToArray();
        Assert.IsTrue(trxCategoriesProperty.Length == 1);
        Assert.IsTrue(trxCategoriesProperty[0].Categories.Length == 1);
        Assert.AreEqual(trxCategoriesProperty[0].Categories[0], "category1");
    }

    public void ToTestNode_WhenTestResultHasTestCaseHierarchyTestProperty_TestNodePropertiesContainItInSerializableNamedArrayStringProperty()
    {
        TestResult testResult = new(new TestCase("SomeFqn", new("executor://uri", UriKind.Absolute), "source.cs"));
        var testCaseHierarchy = TestProperty.Register("TestCase.Hierarchy", "Label", typeof(string[]), TestPropertyAttributes.None, typeof(TestCase));
        testResult.SetPropertyValue<string[]>(testCaseHierarchy, ["assembly", "class", "category", "test"]);

        var testNode = testResult.ToTestNode(false, VSTestClient);

        SerializableNamedArrayStringProperty[] trxCategoriesProperty = testNode.Properties.OfType<SerializableNamedArrayStringProperty>().ToArray();
        Assert.IsTrue(trxCategoriesProperty.Length == 1);
        Assert.AreEqual(trxCategoriesProperty[0].Values[0], "assembly");
        Assert.AreEqual(trxCategoriesProperty[0].Values[1], "class");
        Assert.AreEqual(trxCategoriesProperty[0].Values[2], "category");
        Assert.AreEqual(trxCategoriesProperty[0].Values[3], "test");
    }

    public void ToTestNode_WhenTestResultHasOriginalExecutorUriProperty_TestNodePropertiesContainItInSerializableKeyValuePairStringProperty()
    {
        TestResult testResult = new(new TestCase("SomeFqn", new("executor://uri", UriKind.Absolute), "source.cs"));
        var originalExecutorUriProperty = TestProperty.Register(
        VSTestTestNodeProperties.OriginalExecutorUriPropertyName, VSTestTestNodeProperties.OriginalExecutorUriPropertyName, typeof(Uri), typeof(TestCase));

        testResult.SetPropertyValue<Uri>(originalExecutorUriProperty, new Uri("https://vs.com/"));

        var testNode = testResult.ToTestNode(false, VSTestClient);

        SerializableKeyValuePairStringProperty[] serializableKeyValuePairStringProperty = testNode.Properties.OfType<SerializableKeyValuePairStringProperty>().ToArray();
        Assert.IsTrue(serializableKeyValuePairStringProperty.Length == 3);
        Assert.AreEqual(serializableKeyValuePairStringProperty[0].Key, VSTestTestNodeProperties.OriginalExecutorUriPropertyName);
        Assert.AreEqual(serializableKeyValuePairStringProperty[0].Value, "https://vs.com/");
    }

    public void ToTestNode_WhenTestResultHasFullyQualifiedTypeAndTrxEnabled_TestNodeHasFullyQualifiedTypeName()
    {
        TestResult testResult = new(new TestCase("assembly.class.test", new("executor://uri", UriKind.Absolute), "source.cs"));

        var testNode = testResult.ToTestNode(true, TestClient);

        Assert.AreEqual(testNode.Properties.OfType<TrxExceptionProperty>()?.Length, 1);
        Assert.AreEqual("assembly.class", testNode.Properties.Single<TrxFullyQualifiedTypeNameProperty>().FullyQualifiedTypeName);
    }

    public void ToTestNode_WhenTestResultHasNoFullyQualifiedTypeAndTrxEnabled_Throws()
    {
        TestResult testResult = new(new TestCase("test", new("executor://uri", UriKind.Absolute), "source.cs"));

        string errorMessage = Assert.Throws<InvalidOperationException>(() => testResult.ToTestNode(true, TestClient)).Message;

        Assert.IsTrue(errorMessage.Contains("Unable to parse fully qualified type name from test case: "));
    }

    public void ToTestNode_FromTestResult_TestNodePropertiesContainCorrectTimingProperty()
    {
        var startTime = new DateTime(1996, 8, 22, 20, 30, 5);
        var endTime = new DateTime(1996, 8, 22, 20, 31, 5);
        var duration = new TimeSpan(0, 1, 0);

        TestResult testResult = new(new TestCase("SomeFqn", new("executor://uri", UriKind.Absolute), "source.cs"))
        {
            StartTime = startTime,
            EndTime = endTime,
            Duration = duration,
        };
        var testNode = testResult.ToTestNode(false, TestClient);
        var testResultTimingProperty = new TimingProperty(new(startTime, endTime, duration), []);

        Assert.AreEqual<TimingProperty>(testNode.Properties.OfType<TimingProperty>()[0], testResultTimingProperty);
    }

    public void ToTestNode_WhenTestResultOutcomeIsNotFoundWithoutSetErrorMessage_TestNodePropertiesContainErrorTestNodeStatePropertyWithDefaultErrorMessage()
    {
        TestResult testResult = new(new TestCase("SomeFqn", new("executor://uri", UriKind.Absolute), "source.cs"))
        {
            Outcome = TestOutcome.NotFound,
            ErrorStackTrace = "SomeStackTrace",
        };
        var testNode = testResult.ToTestNode(false, TestClient);

        ErrorTestNodeStateProperty[] errorTestNodeStateProperties = testNode.Properties.OfType<ErrorTestNodeStateProperty>().ToArray();
        Assert.IsTrue(errorTestNodeStateProperties.Length == 1);
        Assert.IsTrue(errorTestNodeStateProperties[0].Exception is VSTestException);
        Assert.AreEqual(testResult.ErrorStackTrace, errorTestNodeStateProperties[0].Exception!.StackTrace);
        Assert.IsTrue(errorTestNodeStateProperties[0].Exception!.Message.Contains("Not found"));
    }

    public void ToTestNode_WhenTestResultOutcomeIsSkipped_TestNodePropertiesContainSkippedTestNodeStateProperty()
    {
        TestResult testResult = new(new TestCase("SomeFqn", new("executor://uri", UriKind.Absolute), "source.cs"))
        {
            Outcome = TestOutcome.Skipped,
        };
        var testNode = testResult.ToTestNode(false, TestClient);

        SkippedTestNodeStateProperty[] skipTestNodeStateProperties = testNode.Properties.OfType<SkippedTestNodeStateProperty>().ToArray();
        Assert.IsTrue(skipTestNodeStateProperties.Length == 1);
    }

    public void ToTestNode_WhenTestResultOutcomeIsNone_TestNodePropertiesContainSkippedTestNodeStateProperty()
    {
        TestResult testResult = new(new TestCase("SomeFqn", new("executor://uri", UriKind.Absolute), "source.cs"))
        {
            Outcome = TestOutcome.None,
        };
        var testNode = testResult.ToTestNode(false, TestClient);

        SkippedTestNodeStateProperty[] skipTestNodeStateProperties = testNode.Properties.OfType<SkippedTestNodeStateProperty>().ToArray();
        Assert.IsTrue(skipTestNodeStateProperties.Length == 1);
    }

    public void ToTestNode_WhenTestResultOutcomeIsPassed_TestNodePropertiesContainPassedTestNodeStateProperty()
    {
        TestResult testResult = new(new TestCase("SomeFqn", new("executor://uri", UriKind.Absolute), "source.cs"))
        {
            Outcome = TestOutcome.Passed,
        };
        var testNode = testResult.ToTestNode(false, TestClient);

        PassedTestNodeStateProperty[] passedTestNodeStateProperties = testNode.Properties.OfType<PassedTestNodeStateProperty>().ToArray();
        Assert.IsTrue(passedTestNodeStateProperties.Length == 1);
    }

    public void ToTestNode_WhenTestResultHasUidAndDisplayNameWithWellKnownClient_TestNodePropertiesContainSerializableKeyValuePairStringPropertyTwice()
    {
        TestResult testResult = new(new TestCase("SomeFqn", new("executor://uri", UriKind.Absolute), "source.cs"))
        {
            DisplayName = "TestName",
        };

        var testNode = testResult.ToTestNode(false, VSTestClient);

        SerializableKeyValuePairStringProperty[] errorTestNodeStateProperties = testNode.Properties.OfType<SerializableKeyValuePairStringProperty>().ToArray();
        Assert.IsTrue(errorTestNodeStateProperties.Length == 2, "Expected 2 SerializableKeyValuePairStringProperty");
        Assert.IsTrue(errorTestNodeStateProperties[0].Key == "vstest.TestCase.Id");
        Assert.IsTrue(errorTestNodeStateProperties[1].Key == "vstest.TestCase.FullyQualifiedName");
        Assert.IsTrue(errorTestNodeStateProperties[1].Value == "SomeFqn");
    }

    public void ToTestNode_WhenTestResultHasTraits_TestNodePropertiesContainIt()
    {
        TestResult testResult = new(new TestCase("SomeFqn", new("executor://uri", UriKind.Absolute), "source.cs"))
        {
            DisplayName = "TestName",
            Traits = { new Trait("key", "value") },
        };

        var testNode = testResult.ToTestNode(false, VSTestClient);

        SerializableNamedKeyValuePairsStringProperty[] errorTestNodeStateProperties = testNode.Properties.OfType<SerializableNamedKeyValuePairsStringProperty>().ToArray();
        Assert.IsTrue(errorTestNodeStateProperties.Length == 1);
        Assert.IsTrue(errorTestNodeStateProperties[0].Name == "traits");
        Assert.IsTrue(errorTestNodeStateProperties[0].Pairs.Length == 1);
        Assert.IsTrue(errorTestNodeStateProperties[0].Pairs[0].Key == "key");
        Assert.IsTrue(errorTestNodeStateProperties[0].Pairs[0].Value == "value");
    }
}
