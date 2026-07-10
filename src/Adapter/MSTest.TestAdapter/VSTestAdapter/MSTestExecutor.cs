// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

/// <summary>
/// Contains the execution logic for this adapter.
/// </summary>
[StackTraceHidden]
internal sealed class MSTestExecutor
{
    private readonly CancellationToken _cancellationToken;
#if !WINDOWS_UWP && !WIN_UI
    private readonly Func<string, IDictionary<string, object>, Task>? _telemetrySender;
#endif

    /// <summary>
    /// Token for canceling the test run.
    /// </summary>
    private TestRunCancellationToken? _testRunCancellationToken;

    internal MSTestExecutor(CancellationToken cancellationToken, Func<string, IDictionary<string, object>, Task>? telemetrySender = null)
    {
        TestExecutionManager = new TestExecutionManager();
        _cancellationToken = cancellationToken;
#if !WINDOWS_UWP && !WIN_UI
        _telemetrySender = telemetrySender;
#else
        _ = telemetrySender;
#endif
    }

    /// <summary>
    /// Gets the ms test execution manager.
    /// </summary>
    internal TestExecutionManager TestExecutionManager { get; }

#pragma warning disable CA2255 // The 'ModuleInitializer' attribute should not be used in libraries
    [ModuleInitializer]
#pragma warning restore CA2255 // The 'ModuleInitializer' attribute should not be used in libraries
    internal static void MSTestModuleInitializer()
        => EnsureAdapterAndFrameworkVersions();

    private static void EnsureAdapterAndFrameworkVersions()
    {
        string? adapterVersion = typeof(MSTestExecutor).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        string? frameworkVersion = typeof(TestMethodAttribute).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (adapterVersion is not null && frameworkVersion is not null
            && adapterVersion != frameworkVersion)
        {
            throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, Resource.VersionMismatchBetweenAdapterAndFramework, adapterVersion, frameworkVersion));
        }
    }

    internal Task RunTestsAsync(IEnumerable<string>? sources, IRunContext? runContext, IFrameworkHandle? frameworkHandle, IConfiguration? configuration, bool isMTP)
        => RunTestsFromSourcesCoreAsync(sources, runContext, frameworkHandle, settings => frameworkHandle!.ToTestResultRecorder(Environment.MachineName, settings), configuration, isMTP);

    /// <summary>
    /// Runs the tests from sources, reporting results to a caller-provided platform-agnostic
    /// <see cref="ITestResultRecorder"/> (used by the native Microsoft.Testing.Platform integration, which
    /// publishes test nodes itself instead of recording through the VSTest <see cref="IFrameworkHandle"/>).
    /// The <paramref name="frameworkHandle"/> is still used for message logging and apartment-state handling.
    /// The recorder is created via <paramref name="recorderFactory"/> after settings are resolved so it observes
    /// the effective <see cref="MSTestSettings"/>.
    /// </summary>
    internal Task RunTestsAsync(IEnumerable<string>? sources, IRunContext? runContext, IFrameworkHandle? frameworkHandle, Func<MSTestSettings, ITestResultRecorder> recorderFactory, IConfiguration? configuration, bool isMTP)
        => RunTestsFromSourcesCoreAsync(sources, runContext, frameworkHandle, recorderFactory, configuration, isMTP);

    private async Task RunTestsFromSourcesCoreAsync(IEnumerable<string>? sources, IRunContext? runContext, IFrameworkHandle? frameworkHandle, Func<MSTestSettings, ITestResultRecorder> recorderFactory, IConfiguration? configuration, bool isMTP)
    {
        if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsInfoEnabled)
        {
            PlatformServiceProvider.Instance.AdapterTraceLogger.Info("MSTestExecutor.RunTests: Running tests from sources.");
        }

        if (frameworkHandle is null)
        {
            throw new ArgumentNullException(nameof(frameworkHandle));
        }

        // VSTest annotates the IEnumerable as nullable; the exact reason is unclear.
        if (sources is null)
        {
            throw new ArgumentNullException(nameof(sources));
        }

        Ensure.NotEmpty(sources);

        // Initialize telemetry collection if not already set
#if !WINDOWS_UWP && !WIN_UI
        if (!MSTestTelemetryDataCollector.IsTelemetryOptedOut())
        {
            _ = MSTestTelemetryDataCollector.EnsureInitialized();
        }
#endif

        try
        {
            TestSourceHandler testSourceHandler = new();
            if (!MSTestDiscovererHelpers.InitializeDiscovery(sources, runContext?.RunSettings?.SettingsXml, frameworkHandle.ToAdapterMessageLogger(), configuration, testSourceHandler))
            {
                return;
            }

            sources = testSourceHandler.GetTestSources(sources);

            // Build the neutral result recorder now that settings have been resolved by InitializeDiscovery; the
            // execution engine reports results through this recorder and never constructs VSTest results. For the
            // VSTest path this wraps the framework handle; for the native MTP path it publishes test nodes directly.
            ITestResultRecorder testResultRecorder = recorderFactory(MSTestSettings.CurrentSettings);

            // Extract the neutral run inputs (test-run directory + run settings XML) from the host run context at
            // this boundary so the execution engine no longer depends on the VSTest run context.
            var deploymentContext = new DeploymentContext(runContext?.TestRunDirectory, runContext?.RunSettings?.SettingsXml);

            await RunTestsFromRightContextAsync(frameworkHandle, async testRunToken => await TestExecutionManager.RunTestsAsync(sources, deploymentContext, frameworkHandle.ToAdapterMessageLogger(), testResultRecorder, new TestElementFilterProvider(runContext), testSourceHandler, isMTP, testRunToken).ConfigureAwait(false)).ConfigureAwait(false);
        }
        finally
        {
            await SendTelemetryAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Cancel the test run.
    /// </summary>
    internal void Cancel()
        => _testRunCancellationToken?.Cancel();

#if !WINDOWS_UWP && !WIN_UI
    private Task SendTelemetryAsync()
        => MSTestTelemetryDataCollector.SendExecutionTelemetryAndResetAsync(_telemetrySender);
#else
    private static Task SendTelemetryAsync()
        => Task.CompletedTask;
#endif

    private async Task RunTestsFromRightContextAsync(IFrameworkHandle frameworkHandle, Func<TestRunCancellationToken, Task> runTestsAction)
    {
        ApartmentState? requestedApartmentState = MSTestSettings.RunConfigurationSettings.ExecutionApartmentState;

        await StaThreadHelper.RunOnApartmentThreadIfNeededAsync(
            requestedApartmentState,
            "MSTest Entry Point",
            async () =>
            {
                await DoRunTestsAsync().ConfigureAwait(false);
                return 0;
            },
            () => 0,
            entryPointThread => Task.Run(entryPointThread.Join, _cancellationToken),
            (_, ex) =>
            {
                frameworkHandle.SendMessage(TestMessageLevel.Error, ex.ToString());
                return 0;
            },
            () => frameworkHandle.SendMessage(
                TestMessageLevel.Warning,
                Resource.STAIsOnlySupportedOnWindowsWarning)).ConfigureAwait(false);

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
