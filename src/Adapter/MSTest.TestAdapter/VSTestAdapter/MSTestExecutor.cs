// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.VSTestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

/// <summary>
/// Contains the execution logic for this adapter.
/// </summary>
[ExtensionUri(EngineConstants.ExecutorUriString)]
[StackTraceHidden]
internal sealed class MSTestExecutor : ITestExecutor
{
    private readonly MSTestEngine _engine;

    /// <summary>
    /// Initializes a new instance of the <see cref="MSTestExecutor"/> class.
    /// </summary>
    public MSTestExecutor()
    {
        TestExecutionManager = new TestExecutionManager();
        _engine = new MSTestEngine(CancellationToken.None, testExecutionManager: TestExecutionManager);
    }

    internal MSTestExecutor(CancellationToken cancellationToken, Func<string, IDictionary<string, object>, Task>? telemetrySender = null)
    {
        TestExecutionManager = new TestExecutionManager();
        _engine = new MSTestEngine(cancellationToken, telemetrySender, TestExecutionManager);
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

        // Unwrap the VSTest run context / framework handle into neutral inputs and delegate to the
        // platform-agnostic engine, which owns telemetry, apartment-state handling and the platform-services
        // execution pipeline. The host test cases are converted into the neutral model lazily (after settings are
        // resolved) so a settings error bails out before any conversion, matching the historical order. Each
        // element keeps its originating test case as an opaque handle plus the host execution-context (TCM)
        // properties surfaced through TestContext.
        await _engine.RunFromTestElementsAsync(
            () => [.. tests.Select(ToUnitTestElement)],
            from test in tests select test.Source,
            runContext?.RunSettings?.SettingsXml,
            runContext?.TestRunDirectory,
            frameworkHandle.ToAdapterMessageLogger(),
            settings => frameworkHandle.ToTestResultRecorder(Environment.MachineName, settings),
            new TestElementFilterProvider(runContext),
            configuration,
            new TestSourceHandler()).ConfigureAwait(false);
    }

    private static UnitTestElement ToUnitTestElement(TestCase testCase)
    {
        UnitTestElement testElement = testCase.ToUnitTestElementWithUpdatedSource(testCase.Source);
        testElement.ExecutionContextProperties = TcmTestPropertiesProvider.GetTcmProperties(testCase);
        testElement.HostRecordingHandle = testCase;
        return testElement;
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

        // TODO: Verify why VSTest annotates the IEnumerable as nullable.
        if (sources is null)
        {
            throw new ArgumentNullException(nameof(sources));
        }

        // Unwrap the VSTest run context / framework handle into neutral inputs and delegate to the
        // platform-agnostic engine. The recorder is created via the factory after settings are resolved so it
        // observes the effective MSTestSettings; for the VSTest path this wraps the framework handle, for the
        // native MTP path it publishes test nodes directly.
        await _engine.RunFromSourcesAsync(
            sources,
            runContext?.RunSettings?.SettingsXml,
            runContext?.TestRunDirectory,
            frameworkHandle.ToAdapterMessageLogger(),
            recorderFactory,
            new TestElementFilterProvider(runContext),
            configuration,
            new TestSourceHandler(),
            isMTP).ConfigureAwait(false);
    }

    /// <summary>
    /// Cancel the test run.
    /// </summary>
    public void Cancel()
        => _engine.Cancel();
}
