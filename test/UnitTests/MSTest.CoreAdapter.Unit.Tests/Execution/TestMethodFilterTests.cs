// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

extern alias FrameworkV1;
extern alias FrameworkV2;
extern alias FrameworkV2CoreExtension;

using System;
using System.Collections.Generic;
using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
using UTF = FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting;
using UTFExtension = FrameworkV2CoreExtension::Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class TestMethodFilterTests
{
    public TestMethodFilterTests()
    {
        TestMethodFilter = new TestMethodFilter();
    }

    private TestMethodFilter TestMethodFilter { get; set; }

    [TestMethod]
    public void PropertyProviderForFullyQualifiedNamePropertyReturnFullyQualifiedNameTestProperty()
    {
        TestProperty property = TestMethodFilter.PropertyProvider("FullyQualifiedName");
        Assert.AreEqual("FullyQualifiedName", property.Label);
    }

    [TestMethod]
    public void PropertyProviderForClassNamePropertyReturnClassNameTestProperty()
    {
        TestProperty property = TestMethodFilter.PropertyProvider("ClassName");
        Assert.AreEqual("ClassName", property.Label);
    }

    [TestMethod]
    public void PropertyProviderForNamePropertyReturnNameTestProperty()
    {
        TestProperty property = TestMethodFilter.PropertyProvider("Name");
        Assert.AreEqual("Name", property.Label);
    }

    [TestMethod]
    public void PropertyProviderForTestCategoryPropertyReturnTestCategoryTestProperty()
    {
        TestProperty property = TestMethodFilter.PropertyProvider("TestCategory");
        Assert.AreEqual("TestCategory", property.Label);
    }

    [TestMethod]
    public void PropertyProviderForPriorityPropertyReturnPriorityTestProperty()
    {
        TestProperty property = TestMethodFilter.PropertyProvider("Priority");
        Assert.AreEqual("Priority", property.Label);
    }

    [TestMethod]
    public void PropertyProviderValueForInvalidTestCaseReturnsNull()
    {
        var result = TestMethodFilter.PropertyValueProvider(null, "Hello");
        Assert.IsNull(result);
    }

    [TestMethod]
    public void PropertyProviderValueForInvalidPropertyNameReturnsNull()
    {
        var type = typeof(DummyTestClassWithTestMethods);
        var fullName = $"{type.FullName}.{"TestMethod"}";
        TestCase testCase = new(fullName, MSTest.TestAdapter.Constants.ExecutorUri, Assembly.GetExecutingAssembly().FullName);

        var result = TestMethodFilter.PropertyValueProvider(testCase, null);
        Assert.IsNull(result);
    }

    [TestMethod]
    public void PropertyProviderValueForSupportedPropertyNameWhichIsNotSetReturnsNull()
    {
        var type = typeof(DummyTestClassWithTestMethods);
        var fullName = $"{type.FullName}.{"TestMethod"}";

        TestCase testCase = new(fullName, MSTest.TestAdapter.Constants.ExecutorUri, Assembly.GetExecutingAssembly().FullName);
        var result = TestMethodFilter.PropertyValueProvider(testCase, "Priority");
        Assert.IsNull(result);
    }

    [TestMethod]
    public void PropertyProviderValueForValidTestAndSupportedPropertyNameReturnsValue()
    {
        var type = typeof(DummyTestClassWithTestMethods);
        var fullName = $"{type.FullName}.{"TestMethod"}";

        TestCase testCase = new(fullName, MSTest.TestAdapter.Constants.ExecutorUri, Assembly.GetExecutingAssembly().FullName);

        var result = TestMethodFilter.PropertyValueProvider(testCase, "FullyQualifiedName");
        Assert.AreEqual(fullName, result);
    }

    [TestMethod]
    public void GetFilterExpressionForNullRunContextReturnsNull()
    {
        TestableTestExecutionRecorder recorder = new();
        var filterExpression = TestMethodFilter.GetFilterExpression(null, recorder, out var filterHasError);

        Assert.IsNull(filterExpression);
        Assert.IsFalse(filterHasError);
    }

    [TestMethod]
    public void GetFilterExpressionForValidRunContextReturnsValidTestCaseFilterExpression()
    {
        TestableTestExecutionRecorder recorder = new();
        var dummyFilterExpression = new TestableTestCaseFilterExpression();
        TestableRunContext runContext = new(() => dummyFilterExpression);
        var filterExpression = TestMethodFilter.GetFilterExpression(runContext, recorder, out var filterHasError);

        Assert.AreEqual(dummyFilterExpression, filterExpression);
        Assert.IsFalse(filterHasError);
    }

    /// <summary>
    /// GetFilterExpression should return valid test case filter expression if DiscoveryContext has GetTestCaseFilter.
    /// </summary>
    [TestMethod]
    public void GetFilterExpressionForDiscoveryContextWithGetTestCaseFilterReturnsValidTestCaseFilterExpression()
    {
        TestableTestExecutionRecorder recorder = new();
        var dummyFilterExpression = new TestableTestCaseFilterExpression();
        TestableDiscoveryContextWithGetTestCaseFilter discoveryContext = new(() => dummyFilterExpression);
        var filterExpression = TestMethodFilter.GetFilterExpression(discoveryContext, recorder, out var filterHasError);

        Assert.AreEqual(dummyFilterExpression, filterExpression);
        Assert.IsFalse(filterHasError);
    }

    /// <summary>
    /// GetFilterExpression should return null test case filter expression in case DiscoveryContext doesn't have GetTestCaseFilter.
    /// </summary>
    [TestMethod]
    public void GetFilterExpressionForDiscoveryContextWithoutGetTestCaseFilterReturnsNullTestCaseFilterExpression()
    {
        TestableTestExecutionRecorder recorder = new();
        TestableDiscoveryContextWithoutGetTestCaseFilter discoveryContext = new();
        var filterExpression = TestMethodFilter.GetFilterExpression(discoveryContext, recorder, out var filterHasError);

        Assert.IsNull(filterExpression);
        Assert.IsFalse(filterHasError);
    }

    [TestMethod]
    public void GetFilterExpressionForRunContextGetTestCaseFilterThrowingExceptionReturnsNullWithFilterHasErrorTrue()
    {
        TestableTestExecutionRecorder recorder = new();
        TestableRunContext runContext = new(() => { throw new TestPlatformFormatException("DummyException"); });
        var filterExpression = TestMethodFilter.GetFilterExpression(runContext, recorder, out var filterHasError);

        Assert.IsNull(filterExpression);
        Assert.IsTrue(filterHasError);
        Assert.AreEqual("DummyException", recorder.Message);
        Assert.AreEqual(TestMessageLevel.Error, recorder.TestMessageLevel);
    }

    /// <summary>
    /// GetFilterExpression should return null filter expression and filterHasError as true in case GetTestCaseFilter throws exception.
    /// </summary>
    [TestMethod]
    public void GetFilterExpressionForDiscoveryContextWithGetTestCaseFilterThrowingExceptionReturnsNullWithFilterHasErrorTrue()
    {
        TestableTestExecutionRecorder recorder = new();
        TestableDiscoveryContextWithGetTestCaseFilter discoveryContext = new(() => { throw new TestPlatformFormatException("DummyException"); });
        var filterExpression = TestMethodFilter.GetFilterExpression(discoveryContext, recorder, out var filterHasError);

        Assert.IsNull(filterExpression);
        Assert.IsTrue(filterHasError);
        Assert.AreEqual("DummyException", recorder.Message);
        Assert.AreEqual(TestMessageLevel.Error, recorder.TestMessageLevel);
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

    private class TestableRunContext : IRunContext
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
            Func<string, TestProperty> propertyProvider)
        {
            return _getFilter();
        }
    }

    private class TestableDiscoveryContextWithGetTestCaseFilter : IDiscoveryContext
    {
        private readonly Func<ITestCaseFilterExpression> _getFilter;

        public TestableDiscoveryContextWithGetTestCaseFilter(Func<ITestCaseFilterExpression> getFilter)
        {
            _getFilter = getFilter;
        }

        public IRunSettings RunSettings { get; }

        public ITestCaseFilterExpression GetTestCaseFilter(
            IEnumerable<string> supportedProperties,
            Func<string, TestProperty> propertyProvider)
        {
            return _getFilter();
        }
    }

    private class TestableDiscoveryContextWithoutGetTestCaseFilter : IDiscoveryContext
    {
        public IRunSettings RunSettings { get; }
    }

    private class TestableTestCaseFilterExpression : ITestCaseFilterExpression
    {
        public string TestCaseFilterValue { get; }

        public bool MatchTestCase(TestCase testCase, Func<string, object> propertyValueProvider)
        {
            throw new NotImplementedException();
        }
    }

    private class DummyTestClassAttribute : UTF.TestClassAttribute
    {
    }
}
