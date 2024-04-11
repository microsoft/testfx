// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
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
    private readonly CancellationToken _cancellationToken;

    /// <summary>
    /// Token for canceling the test run.
    /// </summary>
    private TestRunCancellationToken? _testRunCancellationToken;

    /// <summary>
    /// Initializes a new instance of the <see cref="MSTestExecutor"/> class.
    /// </summary>
    public MSTestExecutor()
    {
        TestExecutionManager = new TestExecutionManager();
        _cancellationToken = CancellationToken.None;
    }

    internal MSTestExecutor(CancellationToken cancellationToken)
    {
        TestExecutionManager = new TestExecutionManager();
        _cancellationToken = cancellationToken;
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

        RunTestsFromRightContext(frameworkHandle, testRunToken => TestExecutionManager.RunTests(tests, runContext, frameworkHandle, testRunToken));
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
        RunTestsFromRightContext(frameworkHandle, testRunToken => TestExecutionManager.RunTests(sources, runContext, frameworkHandle, testRunToken));
    }

    public void Cancel()
        => _testRunCancellationToken?.Cancel();

    private void RunTestsFromRightContext(IFrameworkHandle frameworkHandle, Action<TestRunCancellationToken> runTestsAction)
    {
        ApartmentState? requestedApartmentState = MSTestSettings.RunConfigurationSettings.ExecutionApartmentState;

        // If we are on Windows and the requested apartment state is different from the current apartment state,
        // then run the tests in a new thread.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            && requestedApartmentState is not null
            && Thread.CurrentThread.GetApartmentState() != requestedApartmentState)
        {
            Thread entryPointThread = new(new ThreadStart(DoRunTests))
            {
                Name = "MSTest Entry Point",
            };

            entryPointThread.SetApartmentState(requestedApartmentState.Value);
            entryPointThread.Start();

            try
            {
                var threadTask = Task.Run(entryPointThread.Join, _cancellationToken);
                threadTask.Wait(_cancellationToken);
            }
            catch (Exception ex)
            {
                frameworkHandle.SendMessage(TestMessageLevel.Error, ex.ToString());
            }
        }
        else
        {
            // If the requested apartment state is STA and the OS is not Windows, then warn the user.
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                && requestedApartmentState is ApartmentState.STA)
            {
                frameworkHandle.SendMessage(TestMessageLevel.Warning, Resource.STAIsOnlySupportedOnWindowsWarning);
            }

            DoRunTests();
        }

        // Local functions
        void DoRunTests()
        {
            using (_cancellationToken.Register(Cancel))
            {
                try
                {
                    _testRunCancellationToken = new TestRunCancellationToken();
                    runTestsAction(_testRunCancellationToken);
                }
                finally
                {
                    _testRunCancellationToken = null;
                }
            }
        }
    }
}
