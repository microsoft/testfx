// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.Smoke.E2ETests
{
    using Microsoft.MSTestV2.CLIAutomation;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class TestDataSourceExtensibilityTests : CLITestBase
    {
        private const string TestAssembly = "FxExtensibilityTestProject.dll";

        [TestMethod]
        public void ExecuteTestDataSourceExtensibilityTests()
        {
            this.InvokeVsTestForExecution(new string[] { TestAssembly });
            this.ValidatePassedTestsContain(
                "CustomTestDataSourceTestMethod1 (1,2,3)",
                "CustomTestDataSourceTestMethod1 (4,5,6)");
        }

        /* This test needs a reference to "Microsoft.TestPlatform.AdapterUtilities" and "Microsoft.TestPlatform.ObjectModel" NuGet packages.
         * It's used to debug FQN changes in test discovery and test execution.

        // using System;
        // using System.Collections.Generic;
        // using System.Collections.ObjectModel;
        // using System.Diagnostics;
        // using System.IO;
        // using System.Linq;
        // using System.Reflection;
        // using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
        // using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
        // using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
        // using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
        // using Microsoft.VisualStudio.TestPlatform.ObjectModel;
        // using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
        // using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

        [TestMethod]
        public void DataSourceTest()
        {
            var assemblyPath = Path.IsPathRooted(TestAssembly) ? TestAssembly : this.GetAssetFullPath(TestAssembly);

            var unitTestDiscoverer = new UnitTestDiscoverer();
            var logger = new InternalLogger();
            var sink = new InternalSink();
            string runSettingXml = this.GetRunSettingXml(string.Empty, this.GetTestAdapterPath());
            var context = new InternalDiscoveryContext(runSettingXml);

            unitTestDiscoverer.DiscoverTestsInSource(assemblyPath, logger, sink, context);

            var settings = this.GetSettingsWithDebugTrace(true);
            var unitTestRunner = new UnitTestRunner(settings);
            var testCase = sink.DiscoveredTests.Single(i => i.DisplayName == "CustomTestDataSourceTestMethod1");

            var unitTestElement = testCase.ToUnitTestElement(assemblyPath);
            var testResults = unitTestRunner.RunSingleTest(unitTestElement.TestMethod, new Dictionary<string, object>());

            var passedTestResults = testResults.Where(i => i.Outcome == Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestOutcome.Passed).Select(i => i.ToTestResult(testCase, DateTimeOffset.Now, DateTimeOffset.Now, settings));

            var expectedTests = new[] { "CustomTestDataSourceTestMethod1 (1,2,3)", "CustomTestDataSourceTestMethod1 (4,5,6)" };
            foreach (var test in expectedTests)
            {
                var testFound = passedTestResults.Any(
                    p => test.Equals(p.TestCase?.FullyQualifiedName)
                         || test.Equals(p.DisplayName)
                         || test.Equals(p.TestCase.DisplayName));

                Assert.IsTrue(testFound, "Test '{0}' does not appear in passed tests list.", test);
            }
        }

        private MSTestSettings GetSettingsWithDebugTrace(bool captureDebugTraceValue)
        {
            string runSettingxml =
                 @"<RunSettings>
                     <MSTest>
                        <CaptureTraceOutput>" + captureDebugTraceValue + @"</CaptureTraceOutput>
                     </MSTest>
                   </RunSettings>";

            return MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsName);
        }

        private class InternalLogger : IMessageLogger
        {
            public void SendMessage(TestMessageLevel testMessageLevel, string message)
            {
                Debug.WriteLine($"{testMessageLevel}: {message}");
            }
        }

        private class InternalSink : Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter.ITestCaseDiscoverySink
        {
            private readonly List<TestCase> testCases = new List<TestCase>();

            public ReadOnlyCollection<TestCase> DiscoveredTests => this.testCases.AsReadOnly();

            public void SendTestCase(TestCase discoveredTest) => this.testCases.Add(discoveredTest);
        }

        private class InternalDiscoveryContext : IDiscoveryContext
        {
            private readonly IRunSettings runSettings;

            public InternalDiscoveryContext(string runSettings)
            {
                this.runSettings = new InternalRunSettings(runSettings);
            }

            public IRunSettings RunSettings => this.runSettings;

            private class InternalRunSettings : IRunSettings
            {
                private readonly string runSettings;

                public InternalRunSettings(string runSettings)
                {
                    this.runSettings = runSettings;
                }

                public string SettingsXml => this.runSettings;

                public ISettingsProvider GetSettings(string settingsName)
                {
                    throw new System.NotImplementedException();
                }
            }
        }
        */
    }
}
