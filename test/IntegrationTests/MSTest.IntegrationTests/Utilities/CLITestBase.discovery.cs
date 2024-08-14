// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;

using DiscoveryAndExecutionTests.Utilities;

using FluentAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

using TestFramework.ForTestingMSTest;

namespace Microsoft.MSTestV2.CLIAutomation;

public partial class CLITestBase : TestContainer
{
    internal ImmutableArray<TestCase> DiscoverTests(string assemblyPath, string testCaseFilter = null)
    {
        var unitTestDiscoverer = new UnitTestDiscoverer();
        var logger = new InternalLogger();
        var sink = new InternalSink();

        string runSettingXml = GetRunSettingXml(string.Empty);
        var context = new InternalDiscoveryContext(runSettingXml, testCaseFilter);

        unitTestDiscoverer.DiscoverTestsInSource(assemblyPath, logger, sink, context);

        return sink.DiscoveredTests;
    }

    internal ImmutableArray<TestResult> RunTests(IEnumerable<TestCase> testCases)
    {
        var testExecutionManager = new TestExecutionManager();
        var frameworkHandle = new InternalFrameworkHandle();

        testExecutionManager.ExecuteTests(testCases, null, frameworkHandle, false);
        return frameworkHandle.GetFlattenedTestResults();
    }

    #region Helper classes
    private class InternalLogger : IMessageLogger
    {
        public void SendMessage(TestMessageLevel testMessageLevel, string message) => Debug.WriteLine($"{testMessageLevel}: {message}");
    }

    private sealed class InternalSink : ITestCaseDiscoverySink
    {
        private readonly List<TestCase> _testCases = [];

        public ImmutableArray<TestCase> DiscoveredTests => _testCases.ToImmutableArray();

        public void SendTestCase(TestCase discoveredTest) => _testCases.Add(discoveredTest);
    }

    private class InternalDiscoveryContext : IDiscoveryContext
    {
        private readonly ITestCaseFilterExpression _filter;

        public InternalDiscoveryContext(string runSettings, string testCaseFilter)
        {
            RunSettings = new InternalRunSettings(runSettings);

            if (testCaseFilter != null)
            {
                _filter = TestCaseFilterFactory.ParseTestFilter(testCaseFilter);
            }
        }

        public IRunSettings RunSettings { get; }

        public ITestCaseFilterExpression GetTestCaseFilter(IEnumerable<string> supportedProperties, Func<string, TestProperty> propertyProvider) => _filter;

        private class InternalRunSettings : IRunSettings
        {
            public InternalRunSettings(string runSettings) => SettingsXml = runSettings;

            public string SettingsXml { get; }

            public ISettingsProvider GetSettings(string settingsName) => throw new NotImplementedException();
        }
    }

    private sealed class InternalFrameworkHandle : IFrameworkHandle
    {
        private readonly List<string> _messageList = [];
        private readonly ConcurrentDictionary<TestCase, ConcurrentBag<TestResult>> _testResults = new();

        private ConcurrentBag<TestResult> _activeResults;

        public bool EnableShutdownAfterTestRun { get; set; }

        public void RecordStart(TestCase testCase) => _activeResults = _testResults.GetOrAdd(testCase, _ => new());

        public void RecordEnd(TestCase testCase, TestOutcome outcome) => _activeResults = _testResults[testCase];

        public void RecordResult(TestResult testResult)
        {
            testResult.Should().NotBeNull();
            _activeResults.Add(testResult);
        }

        public ImmutableArray<TestResult> GetFlattenedTestResults()
        {
            var allTestResults = _testResults.SelectMany(i => i.Value).ToImmutableArray();
            allTestResults.Should().NotContainNulls();

            return allTestResults;
        }

        public void RecordAttachments(IList<AttachmentSet> attachmentSets)
            => throw new NotImplementedException();

        public void SendMessage(TestMessageLevel testMessageLevel, string message) => _messageList.Add($"{testMessageLevel}:{message}");

        public int LaunchProcessWithDebuggerAttached(string filePath, string workingDirectory, string arguments, IDictionary<string, string> environmentVariables)
            => throw new NotImplementedException();
    }
    #endregion
}
