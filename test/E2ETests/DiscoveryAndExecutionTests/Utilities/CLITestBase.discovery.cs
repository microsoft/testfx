// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.MSTestV2.CLIAutomation
{
    using DiscoveryAndExecutionTests.Utilities;

    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Linq;

    public partial class CLITestBase
    {
        internal ReadOnlyCollection<TestCase> DiscoverTests(string assemblyPath, string testCaseFilter = null)
        {
            var unitTestDiscoverer = new UnitTestDiscoverer();
            var logger = new InternalLogger();
            var sink = new InternalSink();

            string runSettingXml = this.GetRunSettingXml(string.Empty, this.GetTestAdapterPath());
            var context = new InternalDiscoveryContext(runSettingXml, testCaseFilter);

            unitTestDiscoverer.DiscoverTestsInSource(assemblyPath, logger, sink, context);

            return sink.DiscoveredTests;
        }

        internal ReadOnlyCollection<TestResult> RunTests(string source, IEnumerable<TestCase> testCases)
        {
            var settings = this.GetSettings(true);
            var testExecutionManager = new TestExecutionManager();
            var frameworkHandle = new InternalFrameworkHandle();

            testExecutionManager.ExecuteTests(testCases, null, frameworkHandle, false);
            return frameworkHandle.GetFlattenedTestResults().ToList().AsReadOnly();
        }

        #region Helper classes
        private MSTestSettings GetSettings(bool captureDebugTraceValue)
        {
            string runSettingxml =
                 @"<RunSettings>
                    <RunConfiguration>  
                        <DisableAppDomain>True</DisableAppDomain>   
                    </RunConfiguration>";

            // var path = Path.GetDirectoryName(this.GetAssetFullPath(TestAssembly));

            // runSettingxml += @"  
            //         <MSTestV2>
            //             <AssemblyResolution>
            //                 <Directory path = """ + path + @""" />
            //             </AssemblyResolution>
            //         </MSTestV2>";

            if (captureDebugTraceValue)
            {
                runSettingxml
                 += @"<MSTest>
                        <CaptureTraceOutput>true</CaptureTraceOutput>
                     </MSTest>";
            }

            runSettingxml += @"</RunSettings>";


            return MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsName);
        }

        private class InternalLogger : IMessageLogger
        {
            public void SendMessage(TestMessageLevel testMessageLevel, string message)
            {
                Debug.WriteLine($"{testMessageLevel}: {message}");
            }
        }

        private class InternalSink : ITestCaseDiscoverySink
        {
            private readonly List<TestCase> testCases = new List<TestCase>();

            public ReadOnlyCollection<TestCase> DiscoveredTests => this.testCases.AsReadOnly();

            public void SendTestCase(TestCase discoveredTest) => this.testCases.Add(discoveredTest);
        }

        private class InternalDiscoveryContext : IDiscoveryContext
        {
            private readonly IRunSettings runSettings;
            private readonly ITestCaseFilterExpression filter;

            public InternalDiscoveryContext(string runSettings, string testCaseFilter)
            {
                this.runSettings = new InternalRunSettings(runSettings);

                if (testCaseFilter != null)
                {
                    filter = TestCaseFilterFactory.ParseTestFilter(testCaseFilter);
                }
            }

            public IRunSettings RunSettings => this.runSettings;

            public ITestCaseFilterExpression GetTestCaseFilter(IEnumerable<string> supportedProperties, Func<string, TestProperty> propertyProvider)
            {
                return filter;
            }

            private class InternalRunSettings : IRunSettings
            {
                private readonly string runSettings;

                public InternalRunSettings(string runSettings)
                {
                    this.runSettings = runSettings;
                }

                public string SettingsXml => this.runSettings;

                public ISettingsProvider GetSettings(string settingsName) => throw new NotImplementedException();
            }
        }

        private class InternalFrameworkHandle : IFrameworkHandle
        {
            private readonly List<string> messageList = new List<string>();
            private readonly ConcurrentDictionary<TestCase, List<TestResult>> testResults = new ConcurrentDictionary<TestCase, List<TestResult>>();

            private TestCase activeTest;
            private List<TestResult> activeResults;

            public bool EnableShutdownAfterTestRun { get; set; }

            public void RecordStart(TestCase testCase)
            {
                activeResults = testResults.GetOrAdd(testCase, _ => new List<TestResult>());
                activeTest = testCase;
            }

            public void RecordEnd(TestCase testCase, TestOutcome outcome)
            {
                activeResults = testResults[testCase];
                activeTest = testCase;
            }

            public void RecordResult(TestResult testResult)
            {
                activeResults.Add(testResult);
            }

            public IEnumerable<TestResult> GetFlattenedTestResults() => this.testResults.SelectMany(i => i.Value);
            public IEnumerable<KeyValuePair<TestCase, List<TestResult>>> GetTestResults() => this.testResults;

            public void RecordAttachments(IList<AttachmentSet> attachmentSets) => throw new NotImplementedException();

            public void SendMessage(TestMessageLevel testMessageLevel, string message)
            {
                this.messageList.Add(string.Format("{0}:{1}", testMessageLevel, message));
            }

            public int LaunchProcessWithDebuggerAttached(string filePath, string workingDirectory, string arguments, IDictionary<string, string> environmentVariables) => throw new NotImplementedException();
        }
        #endregion
    }
}
