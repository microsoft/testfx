﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public class TestMethodFilterTests : TestContainer
{
    private readonly TestMethodFilter _testMethodFilter;

    public TestMethodFilterTests() => _testMethodFilter = new TestMethodFilter();

    public void PropertyProviderForFullyQualifiedNamePropertyReturnFullyQualifiedNameTestProperty()
    {
        TestProperty property = _testMethodFilter.PropertyProvider("FullyQualifiedName");
        Verify(property.Label == "FullyQualifiedName");
    }

    public void PropertyProviderForClassNamePropertyReturnClassNameTestProperty()
    {
        TestProperty property = _testMethodFilter.PropertyProvider("ClassName");
        Verify(property.Label == "ClassName");
    }

    public void PropertyProviderForNamePropertyReturnNameTestProperty()
    {
        TestProperty property = _testMethodFilter.PropertyProvider("Name");
        Verify(property.Label == "Name");
    }

    public void PropertyProviderForTestCategoryPropertyReturnTestCategoryTestProperty()
    {
        TestProperty property = _testMethodFilter.PropertyProvider("TestCategory");
        Verify(property.Label == "TestCategory");
    }

    public void PropertyProviderForPriorityPropertyReturnPriorityTestProperty()
    {
        TestProperty property = _testMethodFilter.PropertyProvider("Priority");
        Verify(property.Label == "Priority");
    }

    public void PropertyProviderValueForInvalidTestCaseReturnsNull()
    {
        object? result = _testMethodFilter.PropertyValueProvider(null, "Hello");
        Verify(result is null);
    }

    public void PropertyProviderValueForInvalidPropertyNameReturnsNull()
    {
        Type type = typeof(DummyTestClassWithTestMethods);
        string fullName = $"{type.FullName}.TestMethod";
        TestCase testCase = new(fullName, EngineConstants.ExecutorUri, Assembly.GetExecutingAssembly().FullName!);

        object? result = _testMethodFilter.PropertyValueProvider(testCase, null);
        Verify(result is null);
    }

    public void PropertyProviderValueForSupportedPropertyNameWhichIsNotSetReturnsNull()
    {
        Type type = typeof(DummyTestClassWithTestMethods);
        string fullName = $"{type.FullName}.TestMethod";

        TestCase testCase = new(fullName, EngineConstants.ExecutorUri, Assembly.GetExecutingAssembly().FullName!);
        object? result = _testMethodFilter.PropertyValueProvider(testCase, "Priority");
        Verify(result is null);
    }

    public void PropertyProviderValueForValidTestAndSupportedPropertyNameReturnsValue()
    {
        Type type = typeof(DummyTestClassWithTestMethods);
        string fullName = $"{type.FullName}.TestMethod";

        TestCase testCase = new(fullName, EngineConstants.ExecutorUri, Assembly.GetExecutingAssembly().FullName!);

        object? result = _testMethodFilter.PropertyValueProvider(testCase, "FullyQualifiedName");
        Verify(fullName.Equals(result));
    }

    public void GetFilterExpressionForNullRunContextReturnsNull()
    {
        TestableTestExecutionRecorder recorder = new();
        ITestCaseFilterExpression? filterExpression = _testMethodFilter.GetFilterExpression(null, recorder, out bool filterHasError);

        Verify(filterExpression is null);
        Verify(!filterHasError);
    }

    public void GetFilterExpressionForValidRunContextReturnsValidTestCaseFilterExpression()
    {
        TestableTestExecutionRecorder recorder = new();
        var dummyFilterExpression = new TestableTestCaseFilterExpression();
        TestableRunContext runContext = new(() => dummyFilterExpression);
        ITestCaseFilterExpression? filterExpression = _testMethodFilter.GetFilterExpression(runContext, recorder, out bool filterHasError);

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
        ITestCaseFilterExpression? filterExpression = _testMethodFilter.GetFilterExpression(discoveryContext, recorder, out bool filterHasError);

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
        ITestCaseFilterExpression? filterExpression = _testMethodFilter.GetFilterExpression(discoveryContext, recorder, out bool filterHasError);

        Verify(filterExpression is null);
        Verify(!filterHasError);
    }

    public void GetFilterExpressionForRunContextGetTestCaseFilterThrowingExceptionReturnsNullWithFilterHasErrorTrue()
    {
        TestableTestExecutionRecorder recorder = new();
        TestableRunContext runContext = new(() => throw new TestPlatformFormatException("DummyException"));
        ITestCaseFilterExpression? filterExpression = _testMethodFilter.GetFilterExpression(runContext, recorder, out bool filterHasError);

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
        ITestCaseFilterExpression? filterExpression = _testMethodFilter.GetFilterExpression(discoveryContext, recorder, out bool filterHasError);

        Verify(filterExpression is null);
        Verify(filterHasError);
        Verify(recorder.Message == "DummyException");
        Verify(recorder.TestMessageLevel == TestMessageLevel.Error);
    }

    [DummyTestClass]
    internal class DummyTestClassWithTestMethods
    {
        public TestContext TestContext { get; set; } = null!;

        [TestMethod]
        public void TestMethod()
        {
        }
    }

    private class TestableTestExecutionRecorder : IMessageLogger
    {
        public TestMessageLevel TestMessageLevel { get; set; }

        public string? Message { get; set; }

        public void SendMessage(TestMessageLevel testMessageLevel, string message)
        {
            TestMessageLevel = testMessageLevel;
            Message = message;
        }
    }

    private sealed class TestableRunContext : IRunContext
    {
        private readonly Func<ITestCaseFilterExpression?> _getFilter;

        public TestableRunContext(Func<ITestCaseFilterExpression?> getFilter) => _getFilter = getFilter;

        public IRunSettings? RunSettings { get; }

        public bool KeepAlive { get; }

        public bool InIsolation { get; }

        public bool IsDataCollectionEnabled { get; }

        public bool IsBeingDebugged { get; }

        public string? TestRunDirectory { get; }

        public string? SolutionDirectory { get; }

        public ITestCaseFilterExpression? GetTestCaseFilter(
            IEnumerable<string>? supportedProperties,
            Func<string, TestProperty?> propertyProvider) => _getFilter();
    }

    private class TestableDiscoveryContextWithGetTestCaseFilter : IDiscoveryContext
    {
        private readonly Func<ITestCaseFilterExpression> _getFilter;

        public TestableDiscoveryContextWithGetTestCaseFilter(Func<ITestCaseFilterExpression> getFilter) => _getFilter = getFilter;

        public IRunSettings? RunSettings { get; }

        public ITestCaseFilterExpression GetTestCaseFilter(IEnumerable<string> supportedProperties, Func<string, TestProperty> propertyProvider) => _getFilter();
    }

    private class TestableDiscoveryContextWithoutGetTestCaseFilter : IDiscoveryContext
    {
        public IRunSettings? RunSettings { get; }
    }

    private sealed class TestableTestCaseFilterExpression : ITestCaseFilterExpression
    {
        public string TestCaseFilterValue => null!;

        public bool MatchTestCase(TestCase testCase, Func<string, object?> propertyValueProvider) => throw new NotImplementedException();
    }

    private class DummyTestClassAttribute : TestClassAttribute;
}
