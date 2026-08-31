// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

/// <summary>
/// Platform-agnostic discovery/execution orchestrator for the MSTest adapter. It owns the parts that used to
/// live inside the VSTest <see cref="MSTestDiscoverer"/> / <see cref="MSTestExecutor"/> orchestrators but do not
/// depend on the VSTest object model: telemetry lifetime, the STA/apartment run wrapper, and the calls into the
/// platform-services engine (<see cref="MSTestDiscovererHelpers"/>, <see cref="UnitTestDiscoverer"/>,
/// <see cref="TestExecutionManager"/>).
/// </summary>
/// <remarks>
/// Every input is already neutral (<see cref="IAdapterMessageLogger"/>, <see cref="ITestElementFilterProvider"/>,
/// <see cref="IUnitTestElementSink"/>, <see cref="ITestResultRecorder"/>, run settings XML, test-run directory),
/// so both hosts can reach the engine without one going through the other: the VSTest orchestrators unwrap their
/// <c>IDiscoveryContext</c>/<c>IRunContext</c>/<c>IFrameworkHandle</c> into these neutral inputs, and the native
/// Microsoft.Testing.Platform framework (<c>MSTestTestFramework</c>) builds them directly — it no longer calls the
/// VSTest <see cref="MSTestDiscoverer"/> / <see cref="MSTestExecutor"/> classes.
/// </remarks>
internal sealed class MSTestEngine
{
    private readonly CancellationToken _cancellationToken;
    private readonly TestExecutionManager _testExecutionManager;
#if !WINDOWS_UWP && !WIN_UI
    private readonly Func<string, IDictionary<string, object>, Task>? _telemetrySender;
#endif

    /// <summary>
    /// Token for canceling the test run, set for the duration of a run so <see cref="Cancel"/> can reach it.
    /// </summary>
    private TestRunCancellationToken? _testRunCancellationToken;

    internal MSTestEngine(CancellationToken cancellationToken, Func<string, IDictionary<string, object>, Task>? telemetrySender = null, TestExecutionManager? testExecutionManager = null)
    {
        _cancellationToken = cancellationToken;
        _testExecutionManager = testExecutionManager ?? new TestExecutionManager();
#if !WINDOWS_UWP && !WIN_UI
        _telemetrySender = telemetrySender;
#else
        _ = telemetrySender;
#endif
    }

    /// <summary>
    /// Cancels the in-flight run, if any. Mirrors the VSTest <c>ITestExecutor.Cancel</c> contract for the
    /// orchestrator that forwards to this engine.
    /// </summary>
    internal void Cancel()
        => _testRunCancellationToken?.Cancel();

    /// <summary>
    /// Discovers tests from <paramref name="sources"/>, reporting each discovered element to
    /// <paramref name="elementSink"/>.
    /// </summary>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Instance method by design: part of the MSTestEngine instance surface, and it consumes instance telemetry state on platforms where telemetry is available.")]
    internal async Task DiscoverAsync(
        IEnumerable<string> sources,
        string? settingsXml,
        IAdapterMessageLogger logger,
        IUnitTestElementSink elementSink,
        ITestElementFilterProvider? filterProvider,
        IConfiguration? configuration,
        ITestSourceHandler testSourceHandler,
        bool isMTP)
    {
        EnsureTelemetryInitialized();

        try
        {
            if (MSTestDiscovererHelpers.InitializeDiscovery(sources, settingsXml, logger, configuration, testSourceHandler))
            {
                await new UnitTestDiscoverer(testSourceHandler)
                    .DiscoverTestsAsync(sources, logger, elementSink, settingsXml, filterProvider, isMTP)
                    .ConfigureAwait(false);
            }
        }
        finally
        {
#if !WINDOWS_UWP && !WIN_UI
            // Send the discovery telemetry event ('mstest/discovery'). This always runs at the end of discovery —
            // for discover-only sessions it is the only event; for sessions where a run follows, execution will
            // send a separate 'mstest/sessionexit' event carrying assertion usage.
            await MSTestTelemetryDataCollector.SendDiscoveryTelemetryAndResetAsync(_telemetrySender).ConfigureAwait(false);
#endif
        }
    }

    /// <summary>
    /// Runs tests from <paramref name="sources"/>. The <paramref name="recorderFactory"/> is invoked after settings
    /// are resolved so the recorder observes the effective <see cref="MSTestSettings"/>.
    /// </summary>
    internal async Task RunFromSourcesAsync(
        IEnumerable<string> sources,
        string? settingsXml,
        string? testRunDirectory,
        IAdapterMessageLogger logger,
        Func<MSTestSettings, ITestResultRecorder> recorderFactory,
        ITestElementFilterProvider? filterProvider,
        IConfiguration? configuration,
        ITestSourceHandler testSourceHandler,
        bool isMTP)
    {
        Ensure.NotEmpty(sources);

        EnsureTelemetryInitialized();

        try
        {
            if (!MSTestDiscovererHelpers.InitializeDiscovery(sources, settingsXml, logger, configuration, testSourceHandler))
            {
                return;
            }

            sources = testSourceHandler.GetTestSources(sources);

            // Build the neutral result recorder now that settings have been resolved by InitializeDiscovery; the
            // execution engine reports results through this recorder and never constructs VSTest results. For the
            // VSTest path this wraps the framework handle; for the native MTP path it publishes test nodes directly.
            ITestResultRecorder testResultRecorder = recorderFactory(MSTestSettings.CurrentSettings);

            var deploymentContext = new DeploymentContext(testRunDirectory, settingsXml);

            await RunUnderApartmentAsync(logger, async testRunToken =>
                await _testExecutionManager.RunTestsAsync(sources, deploymentContext, logger, testResultRecorder, filterProvider, testSourceHandler, isMTP, testRunToken).ConfigureAwait(false)).ConfigureAwait(false);
        }
        finally
        {
            await SendExecutionTelemetryAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Runs the already-materialized test elements produced by <paramref name="testElementsFactory"/> (the VSTest
    /// "run selected test cases" path). The factory is invoked only after discovery/settings initialization
    /// succeeds, so a settings error bails out before any element is materialized (matching the historical order).
    /// <paramref name="sourcesForInitialization"/> is used only to initialize discovery/settings.
    /// </summary>
    internal async Task RunFromTestElementsAsync(
        Func<IEnumerable<UnitTestElement>> testElementsFactory,
        IEnumerable<string> sourcesForInitialization,
        string? settingsXml,
        string? testRunDirectory,
        IAdapterMessageLogger logger,
        Func<MSTestSettings, ITestResultRecorder> recorderFactory,
        ITestElementFilterProvider? filterProvider,
        IConfiguration? configuration,
        ITestSourceHandler testSourceHandler)
    {
        EnsureTelemetryInitialized();

        try
        {
            if (!MSTestDiscovererHelpers.InitializeDiscovery(sourcesForInitialization, settingsXml, logger, configuration, testSourceHandler))
            {
                return;
            }

            IEnumerable<UnitTestElement> testElements = testElementsFactory();

            ITestResultRecorder testResultRecorder = recorderFactory(MSTestSettings.CurrentSettings);

            var deploymentContext = new DeploymentContext(testRunDirectory, settingsXml);

            await RunUnderApartmentAsync(logger, async testRunToken =>
                await _testExecutionManager.RunTestsAsync(testElements, deploymentContext, logger, testResultRecorder, filterProvider, testRunToken).ConfigureAwait(false)).ConfigureAwait(false);
        }
        finally
        {
            await SendExecutionTelemetryAsync().ConfigureAwait(false);
        }
    }

    private static void EnsureTelemetryInitialized()
    {
#if !WINDOWS_UWP && !WIN_UI
#if NETCOREAPP
        // Telemetry anonymizes custom test-attribute type names with SHA256, which is not supported
        // on wasi-wasm (https://github.com/dotnet/runtime/issues/99126). Skip telemetry collection
        // there so discovery does not throw a PlatformNotSupportedException; the platform side
        // already skips its own SHA256-based telemetry on wasi for the same reason.
        if (OperatingSystem.IsWasi())
        {
            return;
        }
#endif

        // Initialize telemetry collection if not already set (e.g. first call in the session).
        if (!MSTestTelemetryDataCollector.IsTelemetryOptedOut())
        {
            _ = MSTestTelemetryDataCollector.EnsureInitialized();
        }
#endif
    }

#if !WINDOWS_UWP && !WIN_UI
    private Task SendExecutionTelemetryAsync()
        => MSTestTelemetryDataCollector.SendExecutionTelemetryAndResetAsync(_telemetrySender);
#else
    private static Task SendExecutionTelemetryAsync()
        => Task.CompletedTask;
#endif

    private async Task RunUnderApartmentAsync(IAdapterMessageLogger logger, Func<TestRunCancellationToken, Task> runTestsAction)
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
                logger.SendMessage(MessageLevel.Error, ex.ToString());
                return 0;
            },
            () => logger.SendMessage(
                MessageLevel.Warning,
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
