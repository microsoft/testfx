// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

using DiscoveryAndExecutionTests.Utilities;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

using TestFramework.ForTestingMSTest;

namespace Microsoft.MSTestV2.CLIAutomation;
public partial class CLITestBase : TestContainer
{
    internal ReadOnlyCollection<TestCase> DiscoverTests(string assemblyPath, string testCaseFilter = null)
    {
        var unitTestDiscoverer = new UnitTestDiscoverer();
        var logger = new InternalLogger();
        var sink = new InternalSink();

        string runSettingXml = GetRunSettingXml(string.Empty, GetTestAdapterPath());
        var context = new InternalDiscoveryContext(runSettingXml, testCaseFilter);

        unitTestDiscoverer.DiscoverTestsInSource(assemblyPath, logger, sink, context);

        return sink.DiscoveredTests;
    }

    internal async Task<ReadOnlyCollection<TestResult>> RunTests(IEnumerable<TestCase> testCases)
    {
        var testExecutionManager = new TestExecutionManager();
        var frameworkHandle = new InternalFrameworkHandle();

        await testExecutionManager.ExecuteTests(testCases, null, frameworkHandle, false);
        return frameworkHandle.GetFlattenedTestResults().ToList().AsReadOnly();
    }

    #region Helper classes
    private class InternalLogger : IMessageLogger
    {
        public void SendMessage(TestMessageLevel testMessageLevel, string message)
        {
            Debug.WriteLine($"{testMessageLevel}: {message}");
        }
    }

    private class InternalSink : ITestCaseDiscoverySink
    {
        private readonly List<TestCase> _testCases = new();

        public ReadOnlyCollection<TestCase> DiscoveredTests => _testCases.AsReadOnly();

        public void SendTestCase(TestCase discoveredTest) => _testCases.Add(discoveredTest);
    }

    private class InternalDiscoveryContext : IDiscoveryContext
    {
        private readonly IRunSettings _runSettings;
        private readonly ITestCaseFilterExpression _filter;

        public InternalDiscoveryContext(string runSettings, string testCaseFilter)
        {
            _runSettings = new InternalRunSettings(runSettings);

            if (testCaseFilter != null)
            {
                _filter = TestCaseFilterFactory.ParseTestFilter(testCaseFilter);
            }
        }

        public IRunSettings RunSettings => _runSettings;

        public ITestCaseFilterExpression GetTestCaseFilter(IEnumerable<string> supportedProperties, Func<string, TestProperty> propertyProvider)
        {
            return _filter;
        }

        private class InternalRunSettings : IRunSettings
        {
            private readonly string _runSettings;

            public InternalRunSettings(string runSettings)
            {
                _runSettings = runSettings;
            }

            public string SettingsXml => _runSettings;

            public ISettingsProvider GetSettings(string settingsName) => throw new NotImplementedException();
        }
    }

    private class InternalFrameworkHandle : IFrameworkHandle
    {
        private readonly List<string> _messageList = new();
        private readonly ConcurrentDictionary<TestCase, List<TestResult>> _testResults = new();

        private TestCase _activeTest;
        private List<TestResult> _activeResults;

        public bool EnableShutdownAfterTestRun { get; set; }

        public void RecordStart(TestCase testCase)
        {
            _activeResults = _testResults.GetOrAdd(testCase, _ => new List<TestResult>());
            _activeTest = testCase;
        }

        public void RecordEnd(TestCase testCase, TestOutcome outcome)
        {
            _activeResults = _testResults[testCase];
            _activeTest = testCase;
        }

        public void RecordResult(TestResult testResult)
        {
            _activeResults.Add(testResult);
        }

        public IEnumerable<TestResult> GetFlattenedTestResults() => _testResults.SelectMany(i => i.Value);

        public IEnumerable<KeyValuePair<TestCase, List<TestResult>>> GetTestResults() => _testResults;

        public void RecordAttachments(IList<AttachmentSet> attachmentSets) => throw new NotImplementedException();

        public void SendMessage(TestMessageLevel testMessageLevel, string message)
        {
            _messageList.Add(string.Format("{0}:{1}", testMessageLevel, message));
        }

        public int LaunchProcessWithDebuggerAttached(string filePath, string workingDirectory, string arguments, IDictionary<string, string> environmentVariables) => throw new NotImplementedException();
    }
    #endregion
}
