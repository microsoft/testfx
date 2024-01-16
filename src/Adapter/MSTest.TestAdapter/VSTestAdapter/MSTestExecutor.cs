// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

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
    private TestRunCancellationToken? _cancellationToken;

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

        if (!MSTestDiscovererHelpers.InitializeDiscovery(from test in tests select test.Source, runContext, frameworkHandle))
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

        if (!MSTestDiscovererHelpers.InitializeDiscovery(sources, runContext, frameworkHandle))
        {
            return;
        }

        sources = PlatformServiceProvider.Instance.TestSource.GetTestSources(sources);
        _cancellationToken = new TestRunCancellationToken();
        TestExecutionManager.RunTests(sources, runContext, frameworkHandle, _cancellationToken);

        _cancellationToken = null;
    }

    public void Cancel()
        => _cancellationToken?.Cancel();
}
