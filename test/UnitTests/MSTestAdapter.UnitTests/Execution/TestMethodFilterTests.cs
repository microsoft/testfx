// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

using TestFramework.ForTestingMSTest;

using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;
using UTFExtension = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public class TestMethodFilterTests : TestContainer
{
    public TestMethodFilterTests()
    {
        TestMethodFilter = new TestMethodFilter();
    }

    private TestMethodFilter TestMethodFilter { get; set; }

    public void PropertyProviderForFullyQualifiedNamePropertyReturnFullyQualifiedNameTestProperty()
    {
        TestProperty property = TestMethodFilter.PropertyProvider("FullyQualifiedName");
        Verify(property.Label == "FullyQualifiedName");
    }

    public void PropertyProviderForClassNamePropertyReturnClassNameTestProperty()
    {
        TestProperty property = TestMethodFilter.PropertyProvider("ClassName");
        Verify(property.Label == "ClassName");
    }

    public void PropertyProviderForNamePropertyReturnNameTestProperty()
    {
        TestProperty property = TestMethodFilter.PropertyProvider("Name");
        Verify(property.Label == "Name");
    }

    public void PropertyProviderForTestCategoryPropertyReturnTestCategoryTestProperty()
    {
        TestProperty property = TestMethodFilter.PropertyProvider("TestCategory");
        Verify(property.Label == "TestCategory");
    }

    public void PropertyProviderForPriorityPropertyReturnPriorityTestProperty()
    {
        TestProperty property = TestMethodFilter.PropertyProvider("Priority");
        Verify(property.Label == "Priority");
    }

    public void PropertyProviderValueForInvalidTestCaseReturnsNull()
    {
        object result = TestMethodFilter.PropertyValueProvider(null, "Hello");
        Verify(result is null);
    }

    public void PropertyProviderValueForInvalidPropertyNameReturnsNull()
    {
        Type type = typeof(DummyTestClassWithTestMethods);
        string fullName = $"{type.FullName}.{"TestMethod"}";
        TestCase testCase = new(fullName, MSTest.TestAdapter.Constants.ExecutorUri, Assembly.GetExecutingAssembly().FullName);

        object result = TestMethodFilter.PropertyValueProvider(testCase, null);
        Verify(result is null);
    }

    public void PropertyProviderValueForSupportedPropertyNameWhichIsNotSetReturnsNull()
    {
        Type type = typeof(DummyTestClassWithTestMethods);
        string fullName = $"{type.FullName}.{"TestMethod"}";

        TestCase testCase = new(fullName, MSTest.TestAdapter.Constants.ExecutorUri, Assembly.GetExecutingAssembly().FullName);
        object result = TestMethodFilter.PropertyValueProvider(testCase, "Priority");
        Verify(result is null);
    }

    public void PropertyProviderValueForValidTestAndSupportedPropertyNameReturnsValue()
    {
        Type type = typeof(DummyTestClassWithTestMethods);
        string fullName = $"{type.FullName}.{"TestMethod"}";

        TestCase testCase = new(fullName, MSTest.TestAdapter.Constants.ExecutorUri, Assembly.GetExecutingAssembly().FullName);

        object result = TestMethodFilter.PropertyValueProvider(testCase, "FullyQualifiedName");
        Verify(fullName.Equals(result));
    }

    public void GetFilterExpressionForNullRunContextReturnsNull()
    {
        TestableTestExecutionRecorder recorder = new();
        ITestCaseFilterExpression filterExpression = TestMethodFilter.GetFilterExpression(null, recorder, out bool filterHasError);

        Verify(filterExpression is null);
        Verify(!filterHasError);
    }

    public void GetFilterExpressionForValidRunContextReturnsValidTestCaseFilterExpression()
    {
        TestableTestExecutionRecorder recorder = new();
        var dummyFilterExpression = new TestableTestCaseFilterExpression();
        TestableRunContext runContext = new(() => dummyFilterExpression);
        ITestCaseFilterExpression filterExpression = TestMethodFilter.GetFilterExpression(runContext, recorder, out bool filterHasError);

        Verify(dummyFilterExpression == filterExpression);
        Verify(!filterHasError);
    }

    /// <summary>
    /// GetFilterExpression should return valid test case filter expression if DiscoveryContext has GetTestCaseFilter.
    /// </summary>
    public void GetFilterExpressionForDiscoveryContextWithGetTestCaseFilterReturnsValidTestCaseFilterExpression()
    {
        TestableTestExecutionRecorder recorder = new();
        var dummyFilterExpression = new TestableTestCaseFilterExpression();
        TestableDiscoveryContextWithGetTestCaseFilter discoveryContext = new(() => dummyFilterExpression);
        ITestCaseFilterExpression filterExpression = TestMethodFilter.GetFilterExpression(discoveryContext, recorder, out bool filterHasError);

        Verify(dummyFilterExpression == filterExpression);
        Verify(!filterHasError);
    }

    /// <summary>
    /// GetFilterExpression should return null test case filter expression in case DiscoveryContext doesn't have GetTestCaseFilter.
    /// </summary>
    public void GetFilterExpressionForDiscoveryContextWithoutGetTestCaseFilterReturnsNullTestCaseFilterExpression()
    {
        TestableTestExecutionRecorder recorder = new();
        TestableDiscoveryContextWithoutGetTestCaseFilter discoveryContext = new();
        ITestCaseFilterExpression filterExpression = TestMethodFilter.GetFilterExpression(discoveryContext, recorder, out bool filterHasError);

        Verify(filterExpression is null);
        Verify(!filterHasError);
    }

    public void GetFilterExpressionForRunContextGetTestCaseFilterThrowingExceptionReturnsNullWithFilterHasErrorTrue()
    {
        TestableTestExecutionRecorder recorder = new();
        TestableRunContext runContext = new(() => throw new TestPlatformFormatException("DummyException"));
        ITestCaseFilterExpression filterExpression = TestMethodFilter.GetFilterExpression(runContext, recorder, out bool filterHasError);

        Verify(filterExpression is null);
        Verify(filterHasError);
        Verify(recorder.Message == "DummyException");
        Verify(recorder.TestMessageLevel == TestMessageLevel.Error);
    }

    /// <summary>
    /// GetFilterExpression should return null filter expression and filterHasError as true in case GetTestCaseFilter throws exception.
    /// </summary>
    public void GetFilterExpressionForDiscoveryContextWithGetTestCaseFilterThrowingExceptionReturnsNullWithFilterHasErrorTrue()
    {
        TestableTestExecutionRecorder recorder = new();
        TestableDiscoveryContextWithGetTestCaseFilter discoveryContext = new(() => throw new TestPlatformFormatException("DummyException"));
        ITestCaseFilterExpression filterExpression = TestMethodFilter.GetFilterExpression(discoveryContext, recorder, out bool filterHasError);

        Verify(filterExpression is null);
        Verify(filterHasError);
        Verify(recorder.Message == "DummyException");
        Verify(recorder.TestMessageLevel == TestMessageLevel.Error);
    }

    [DummyTestClass]
    internal class DummyTestClassWithTestMethods
    {
        public UTFExtension.TestContext TestContext { get; set; }

        [UTF.TestMethod]
        public void TestMethod()
        {
        }
    }

    private class TestableTestExecutionRecorder : IMessageLogger
    {
        public TestMessageLevel TestMessageLevel { get; set; }

        public string Message { get; set; }

        public void SendMessage(TestMessageLevel testMessageLevel, string message)
        {
            TestMessageLevel = testMessageLevel;
            Message = message;
        }
    }

    private sealed class TestableRunContext : IRunContext
    {
        private readonly Func<ITestCaseFilterExpression> _getFilter;

        public TestableRunContext(Func<ITestCaseFilterExpression> getFilter)
        {
            _getFilter = getFilter;
        }

        public IRunSettings RunSettings { get; }

        public bool KeepAlive { get; }

        public bool InIsolation { get; }

        public bool IsDataCollectionEnabled { get; }

        public bool IsBeingDebugged { get; }

        public string TestRunDirectory { get; }

        public string SolutionDirectory { get; }

        public ITestCaseFilterExpression GetTestCaseFilter(
            IEnumerable<string> supportedProperties,
            Func<string, TestProperty> propertyProvider) => _getFilter();
    }

    private class TestableDiscoveryContextWithGetTestCaseFilter : IDiscoveryContext
    {
        private readonly Func<ITestCaseFilterExpression> _getFilter;

        public TestableDiscoveryContextWithGetTestCaseFilter(Func<ITestCaseFilterExpression> getFilter)
        {
            _getFilter = getFilter;
        }

        public IRunSettings RunSettings { get; }

        public ITestCaseFilterExpression GetTestCaseFilter(IEnumerable<string> supportedProperties, Func<string, TestProperty> propertyProvider) => _getFilter();
    }

    private class TestableDiscoveryContextWithoutGetTestCaseFilter : IDiscoveryContext
    {
        public IRunSettings RunSettings { get; }
    }

    private sealed class TestableTestCaseFilterExpression : ITestCaseFilterExpression
    {
        public string TestCaseFilterValue { get; }

        public bool MatchTestCase(TestCase testCase, Func<string, object> propertyValueProvider) => throw new NotImplementedException();
    }

    private class DummyTestClassAttribute : UTF.TestClassAttribute;
}
