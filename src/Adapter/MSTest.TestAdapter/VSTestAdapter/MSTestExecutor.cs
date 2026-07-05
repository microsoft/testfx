// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.VSTestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

/// <summary>
/// Contains the execution logic for this adapter.
/// </summary>
[ExtensionUri(EngineConstants.ExecutorUriString)]
[StackTraceHidden]
internal sealed class MSTestExecutor : ITestExecutor
{
    private readonly CancellationToken _cancellationToken;
#if !WINDOWS_UWP && !WIN_UI
    private readonly Func<string, IDictionary<string, object>, Task>? _telemetrySender;
#endif

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
    {
        SetPlatformLogger();
        EnsureAdapterAndFrameworkVersions();
    }

    private static void SetPlatformLogger()
        // We set the logger to the VSTest EqtTrace logger as soon as possible via ModuleInitializer.
        // If MTP is used, this will get replaced later.
        => PlatformServiceProvider.Instance.AdapterTraceLogger = EqtTraceLogger.Instance;

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

    /// <summary>
    /// Runs the tests.
    /// </summary>
    /// <param name="tests">The collection of test cases to run.</param>
    /// <param name="runContext">The run context.</param>
    /// <param name="frameworkHandle">The handle to the framework.</param>
#if DEBUG && NET8_0_OR_GREATER
    [Obsolete("Use RunTestsAsync instead.", DiagnosticId = "MSTEST0106", UrlFormat = "https://aka.ms/mstest/diagnostics#{0}")]
#elif DEBUG
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
#if DEBUG && NET8_0_OR_GREATER
    [Obsolete("Use RunTestsAsync instead.", DiagnosticId = "MSTEST0106", UrlFormat = "https://aka.ms/mstest/diagnostics#{0}")]
#elif DEBUG
    [Obsolete("Use RunTestsAsync instead.")]
#endif
    public void RunTests(IEnumerable<string>? sources, IRunContext? runContext, IFrameworkHandle? frameworkHandle)
        => RunTestsAsync(sources, runContext, frameworkHandle, null, false).GetAwaiter().GetResult();

    internal async Task RunTestsAsync(IEnumerable<TestCase>? tests, IRunContext? runContext, IFrameworkHandle? frameworkHandle, IConfiguration? configuration)
    {
        if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsInfoEnabled)
        {
            PlatformServiceProvider.Instance.AdapterTraceLogger.Info("MSTestExecutor.RunTests: Running tests from testcases.");
        }

        if (frameworkHandle is null)
        {
            throw new ArgumentNullException(nameof(frameworkHandle));
        }

        // TODO: Verify why VSTest annotates the IEnumerable as nullable.
        if (tests is null)
        {
            throw new ArgumentNullException(nameof(tests));
        }

        Ensure.NotEmpty(tests);

        // Initialize telemetry collection if not already set
#if !WINDOWS_UWP && !WIN_UI
        if (!MSTestTelemetryDataCollector.IsTelemetryOptedOut())
        {
            _ = MSTestTelemetryDataCollector.EnsureInitialized();
        }
#endif

        try
        {
            if (!MSTestDiscovererHelpers.InitializeDiscovery(from test in tests select test.Source, runContext?.RunSettings?.SettingsXml, frameworkHandle.ToAdapterMessageLogger(), configuration, new TestSourceHandler()))
            {
                return;
            }

            // Convert the host test cases into the neutral model at this boundary so the execution engine runs
            // on UnitTestElement end-to-end. Each element keeps its originating test case as an opaque handle
            // (for full-fidelity result recording and deployment) plus the host execution-context (TCM)
            // properties surfaced through TestContext.
            UnitTestElement[] testElements = [.. tests.Select(ToUnitTestElement)];

            // Translate the VSTest recorder into the platform-agnostic result recorder at this boundary; the
            // execution engine reports results through this neutral recorder and never constructs VSTest results.
            ITestResultRecorder testResultRecorder = frameworkHandle.ToTestResultRecorder(Environment.MachineName, MSTestSettings.CurrentSettings);

            // Extract the neutral run inputs (test-run directory + run settings XML) from the host run context at
            // this boundary so the execution engine no longer depends on the VSTest run context.
            var deploymentContext = new DeploymentContext(runContext?.TestRunDirectory, runContext?.RunSettings?.SettingsXml);

            await RunTestsFromRightContextAsync(frameworkHandle, async testRunToken => await TestExecutionManager.RunTestsAsync(testElements, deploymentContext, frameworkHandle, testResultRecorder, new TestElementFilterProvider(runContext), testRunToken).ConfigureAwait(false)).ConfigureAwait(false);
        }
        finally
        {
            await SendTelemetryAsync().ConfigureAwait(false);
        }
    }

    private static UnitTestElement ToUnitTestElement(TestCase testCase)
    {
        UnitTestElement testElement = testCase.ToUnitTestElementWithUpdatedSource(testCase.Source);
        testElement.ExecutionContextProperties = TcmTestPropertiesProvider.GetTcmProperties(testCase);
        testElement.HostRecordingHandle = testCase;
        return testElement;
    }

    internal async Task RunTestsAsync(IEnumerable<string>? sources, IRunContext? runContext, IFrameworkHandle? frameworkHandle, IConfiguration? configuration, bool isMTP)
    {
        if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsInfoEnabled)
        {
            PlatformServiceProvider.Instance.AdapterTraceLogger.Info("MSTestExecutor.RunTests: Running tests from sources.");
        }

        if (frameworkHandle is null)
        {
            throw new ArgumentNullException(nameof(frameworkHandle));
        }

        // TODO: Verify why VSTest annotates the IEnumerable as nullable.
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

            // Translate the VSTest recorder into the platform-agnostic result recorder at this boundary; the
            // execution engine reports results through this neutral recorder and never constructs VSTest results.
            ITestResultRecorder testResultRecorder = frameworkHandle.ToTestResultRecorder(Environment.MachineName, MSTestSettings.CurrentSettings);

            // Extract the neutral run inputs (test-run directory + run settings XML) from the host run context at
            // this boundary so the execution engine no longer depends on the VSTest run context.
            var deploymentContext = new DeploymentContext(runContext?.TestRunDirectory, runContext?.RunSettings?.SettingsXml);

            await RunTestsFromRightContextAsync(frameworkHandle, async testRunToken => await TestExecutionManager.RunTestsAsync(sources, deploymentContext, frameworkHandle, testResultRecorder, new TestElementFilterProvider(runContext), testSourceHandler, isMTP, testRunToken).ConfigureAwait(false)).ConfigureAwait(false);
        }
        finally
        {
            await SendTelemetryAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Cancel the test run.
    /// </summary>
    public void Cancel()
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
