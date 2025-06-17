// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

/// <summary>
/// Contains the execution logic for this adapter.
/// </summary>
[ExtensionUri(EngineConstants.ExecutorUriString)]
internal sealed class MSTestExecutor : ITestExecutor
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
    /// Gets the ms test execution manager.
    /// </summary>
    public TestExecutionManager TestExecutionManager { get; }

#pragma warning disable CA2255 // The 'ModuleInitializer' attribute should not be used in libraries
    [ModuleInitializer]
#pragma warning restore CA2255 // The 'ModuleInitializer' attribute should not be used in libraries
    internal static void EnsureAdapterAndFrameworkVersions()
    {
        string? adapterVersion = typeof(MSTestExecutor).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        string? frameworkVersion = typeof(TestMethodAttribute).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (adapterVersion is not null && frameworkVersion is not null
            && adapterVersion != frameworkVersion)
        {
            throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, Resource.VersionMismatchBetweenAdapterAndFramework, adapterVersion, frameworkVersion));
        }
    }

    /// <summary>
    /// Runs the tests.
    /// </summary>
    /// <param name="tests">The collection of test cases to run.</param>
    /// <param name="runContext">The run context.</param>
    /// <param name="frameworkHandle">The handle to the framework.</param>
#if DEBUG
    [Obsolete("Use RunTestsAsync instead.")]
#endif
    public void RunTests(IEnumerable<TestCase>? tests, IRunContext? runContext, IFrameworkHandle? frameworkHandle)
        => RunTestsAsync(tests, runContext, frameworkHandle, null).GetAwaiter().GetResult();

    /// <summary>
    /// Runs the tests.
    /// </summary>
    /// <param name="sources">The collection of assemblies to run.</param>
    /// <param name="runContext">The run context.</param>
    /// <param name="frameworkHandle">The handle to the framework.</param>
#if DEBUG
    [Obsolete("Use RunTestsAsync instead.")]
#endif
    public void RunTests(IEnumerable<string>? sources, IRunContext? runContext, IFrameworkHandle? frameworkHandle)
        => RunTestsAsync(sources, runContext, frameworkHandle, null).GetAwaiter().GetResult();

    internal async Task RunTestsAsync(IEnumerable<TestCase>? tests, IRunContext? runContext, IFrameworkHandle? frameworkHandle, IConfiguration? configuration)
    {
        PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo("MSTestExecutor.RunTests: Running tests from testcases.");
        Guard.NotNull(frameworkHandle);
        Guard.NotNullOrEmpty(tests);

        if (!MSTestDiscovererHelpers.InitializeDiscovery(from test in tests select test.Source, runContext, frameworkHandle, configuration))
        {
            return;
        }

        await RunTestsFromRightContextAsync(frameworkHandle, async testRunToken => await TestExecutionManager.RunTestsAsync(tests, runContext, frameworkHandle, testRunToken).ConfigureAwait(false)).ConfigureAwait(false);
    }

    internal async Task RunTestsAsync(IEnumerable<string>? sources, IRunContext? runContext, IFrameworkHandle? frameworkHandle, IConfiguration? configuration)
    {
        PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo("MSTestExecutor.RunTests: Running tests from sources.");
        Guard.NotNull(frameworkHandle);
        Guard.NotNullOrEmpty(sources);
        if (!MSTestDiscovererHelpers.InitializeDiscovery(sources, runContext, frameworkHandle, configuration))
        {
            return;
        }

        sources = PlatformServiceProvider.Instance.TestSource.GetTestSources(sources);
        await RunTestsFromRightContextAsync(frameworkHandle, async testRunToken => await TestExecutionManager.RunTestsAsync(sources, runContext, frameworkHandle, testRunToken).ConfigureAwait(false)).ConfigureAwait(false);
    }

    /// <summary>
    /// Cancel the test run.
    /// </summary>
    public void Cancel()
        => _testRunCancellationToken?.Cancel();

    private async Task RunTestsFromRightContextAsync(IFrameworkHandle frameworkHandle, Func<TestRunCancellationToken, Task> runTestsAction)
    {
        ApartmentState? requestedApartmentState = MSTestSettings.RunConfigurationSettings.ExecutionApartmentState;

        // If we are on Windows and the requested apartment state is different from the current apartment state,
        // then run the tests in a new thread.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            && requestedApartmentState is not null
            && Thread.CurrentThread.GetApartmentState() != requestedApartmentState)
        {
            Thread entryPointThread = new(() => DoRunTestsAsync().GetAwaiter().GetResult())
            {
                Name = "MSTest Entry Point",
            };

            entryPointThread.SetApartmentState(requestedApartmentState.Value);
            entryPointThread.Start();

            try
            {
                var threadTask = Task.Run(entryPointThread.Join, _cancellationToken);
                await threadTask.ConfigureAwait(false);
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

            await DoRunTestsAsync().ConfigureAwait(false);
        }

        // Local functions
        async Task DoRunTestsAsync()
        {
            using (_cancellationToken.Register(Cancel))
            {
                try
                {
                    _testRunCancellationToken = new TestRunCancellationToken(_cancellationToken);
                    await runTestsAction(_testRunCancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    _testRunCancellationToken = null;
                }
            }
        }
    }
}
