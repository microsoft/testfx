// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

/// <summary>
/// Contains the execution logic for this adapter.
/// </summary>
[ExtensionUri(Constants.ExecutorUriString)]
public class MSTestExecutor : ITestExecutor
{
    /// <summary>
    /// Token for canceling the test run.
    /// </summary>
    private TestRunCancellationToken? _cancellationToken = null;

    /// <summary>
    /// Initializes a new instance of the <see cref="MSTestExecutor"/> class.
    /// </summary>
    public MSTestExecutor()
    {
        TestExecutionManager = new TestExecutionManager();
    }

    /// <summary>
    /// Gets or sets the ms test execution manager.
    /// </summary>
    public TestExecutionManager TestExecutionManager { get; protected set; }

    public void RunTests(IEnumerable<TestCase>? tests, IRunContext? runContext, IFrameworkHandle? frameworkHandle)
    {
        PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo("MSTestExecutor.RunTests: Running tests from testcases.");

        ValidateArg.NotNull(frameworkHandle, "frameworkHandle");
        ValidateArg.NotNullOrEmpty(tests, "tests");

        if (!MSTestDiscoverer.AreValidSources(from test in tests select test.Source))
        {
            throw new NotSupportedException();
        }

        // Populate the runsettings.
        try
        {
            // We don't call here tha validateSettings function as we alredy call it in the discover tests.
            MSTestSettings.PopulateSettings(runContext);
        }
        catch (AdapterSettingsException ex)
        {
            frameworkHandle.SendMessage(TestMessageLevel.Error, ex.Message);
            return;
        }

        // Scenarios that include testsettings or forcing a run via the legacy adapter are currently not supported in MSTestAdapter.
        if (MSTestSettings.IsLegacyScenario(frameworkHandle))
        {
            return;
        }

        _cancellationToken = new TestRunCancellationToken();
        TestExecutionManager.RunTests(tests, runContext, frameworkHandle, _cancellationToken);
        _cancellationToken = null;
    }

    public void RunTests(IEnumerable<string>? sources, IRunContext? runContext, IFrameworkHandle? frameworkHandle)
    {
        PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo("MSTestExecutor.RunTests: Running tests from sources.");
        ValidateArg.NotNull(frameworkHandle, "frameworkHandle");
        ValidateArg.NotNullOrEmpty(sources, "sources");

        if (!MSTestDiscoverer.AreValidSources(sources))
        {
            throw new NotSupportedException();
        }

        // Populate the runsettings.
        try
        {
            MSTestSettings.PopulateSettings(runContext);
            MSTestSettings.ValidateSettings(frameworkHandle);
        }
        catch (AdapterSettingsException ex)
        {
            frameworkHandle.SendMessage(TestMessageLevel.Error, ex.Message);
            return;
        }

        // Scenarios that include testsettings or forcing a run via the legacy adapter are currently not supported in MSTestAdapter.
        if (MSTestSettings.IsLegacyScenario(frameworkHandle))
        {
            return;
        }

        sources = PlatformServiceProvider.Instance.TestSource.GetTestSources(sources);
        _cancellationToken = new TestRunCancellationToken();
        TestExecutionManager.RunTests(sources, runContext, frameworkHandle, _cancellationToken);

        _cancellationToken = null;
    }

    public void Cancel()
    {
        _cancellationToken?.Cancel();
    }
}
