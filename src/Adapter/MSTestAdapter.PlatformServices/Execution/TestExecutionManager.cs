// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
#if !WINDOWS_UWP && !WIN_UI
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;
#endif
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

/// <summary>
/// Class responsible for execution of tests at assembly level and sending tests via framework handle.
/// </summary>
#pragma warning disable CA1852 // Seal internal types - This class is inherited in tests.
[StackTraceHidden]
internal partial class TestExecutionManager
{
    /// <summary>
    /// Dictionary for test run parameters.
    /// </summary>
    private readonly IDictionary<string, object> _sessionParameters;
    private readonly Func<Func<Task>, Task> _taskFactory;

    /// <summary>
    /// Specifies whether the test run is canceled or not.
    /// </summary>
    private TestRunCancellationToken? _testRunCancellationToken;

    /// <summary>
    /// Random number generator used to shuffle test execution order when <see cref="MSTestSettings.RandomizeTestOrder"/> is enabled.
    /// Mutated only on the source-processing thread before parallel workers are started.
    /// </summary>
    private Random? _testOrderRandom;

    /// <summary>
    /// Recorder used to report test start/end/results back to the host. Supplied at the adapter boundary and set
    /// once per run in <see cref="ExecuteTestsAsync"/> before any source is processed.
    /// </summary>
    private ITestResultRecorder _testResultRecorder = null!;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestExecutionManager"/> class.
    /// </summary>
    public TestExecutionManager()
        : this(null)
    {
    }

    internal TestExecutionManager(Func<Func<Task>, Task>? taskFactory = null)
    {
        _sessionParameters = new Dictionary<string, object>();
        _taskFactory = taskFactory ?? DefaultFactoryAsync;
    }

    private static Task DefaultFactoryAsync(Func<Task> taskGetter)
    {
        if (MSTestSettings.RunConfigurationSettings.ExecutionApartmentState == ApartmentState.STA
            && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // This is best we can do to execute in STA thread.
            return StaThreadHelper.RunOnStaThreadAsync(taskGetter);
        }
        else
        {
            // NOTE: If you replace this with `return taskGetter()`, you will break parallel tests.
            return Task.Run(taskGetter);
        }
    }

    /// <summary>
    /// Provider for the test filter, supplied at the adapter boundary and set once per run in
    /// <see cref="ExecuteTestsAsync"/> so the filter is built at the same point (and with the same per-source
    /// timing) as before.
    /// </summary>
    private ITestElementFilterProvider? _testElementFilterProvider;

#if !WINDOWS_UWP && !WIN_UI
    /// <summary>
    /// Gets or sets a value indicating whether any test executed has failed.
    /// </summary>
    private bool _hasAnyTestFailed;
#endif

    /// <summary>
    /// Runs the tests.
    /// </summary>
    /// <param name="tests">Tests to be run.</param>
    /// <param name="runContext">Context to use when executing the tests.</param>
    /// <param name="frameworkHandle">Handle to the framework to record results and to do framework operations.</param>
    /// <param name="testResultRecorder">Recorder used to report test results back to the host.</param>
    /// <param name="filterProvider">Provider for the test filter, or <see langword="null"/> for no filter.</param>
    /// <param name="runCancellationToken">Test run cancellation token.</param>
    internal async Task RunTestsAsync(IEnumerable<UnitTestElement> tests, IRunContext? runContext, IFrameworkHandle frameworkHandle, ITestResultRecorder testResultRecorder, ITestElementFilterProvider? filterProvider, TestRunCancellationToken runCancellationToken)
    {
        DebugEx.Assert(tests != null, "tests");
        DebugEx.Assert(runContext != null, "runContext");
        DebugEx.Assert(frameworkHandle != null, "frameworkHandle");
        DebugEx.Assert(runCancellationToken != null, "runCancellationToken");

        _testRunCancellationToken = runCancellationToken;
        PlatformServiceProvider.Instance.TestRunCancellationToken = _testRunCancellationToken;

#if !WINDOWS_UWP && !WIN_UI
        bool isDeploymentDone = Deploy(tests, runContext, frameworkHandle);
#else
        const bool isDeploymentDone = false;
#endif

        // Placing this after deployment since we need information post deployment that we pass in as properties.
        CacheSessionParameters(runContext?.RunSettings?.SettingsXml, frameworkHandle.ToAdapterMessageLogger());

        // Execute the tests
        await ExecuteTestsAsync(tests, runContext, frameworkHandle, testResultRecorder, filterProvider, isDeploymentDone).ConfigureAwait(false);

#if !WINDOWS_UWP && !WIN_UI
        if (!_hasAnyTestFailed)
        {
            PlatformServiceProvider.Instance.TestDeployment.Cleanup();
        }
#endif
    }

    internal async Task RunTestsAsync(IEnumerable<string> sources, IRunContext? runContext, IFrameworkHandle frameworkHandle, ITestResultRecorder testResultRecorder, ITestElementFilterProvider? filterProvider, ITestSourceHandler testSourceHandler, bool isMTP, TestRunCancellationToken cancellationToken)
    {
        _testRunCancellationToken = cancellationToken;
        PlatformServiceProvider.Instance.TestRunCancellationToken = _testRunCancellationToken;

        var discoverySink = new TestCaseDiscoverySink();

        var tests = new List<UnitTestElement>();

        IAdapterMessageLogger logger = frameworkHandle.ToAdapterMessageLogger();

        // deploy everything first.
        foreach (string source in sources)
        {
            _testRunCancellationToken?.ThrowIfCancellationRequested();

            // discover the tests
            GetUnitTestDiscoverer(testSourceHandler).DiscoverTestsInSource(source, logger, discoverySink, runContext, filterProvider, isMTP);
            tests.AddRange(discoverySink.TestElements);

            // Clear discoverSinksTests so that it just stores test for one source at one point of time
            discoverySink.TestElements.Clear();
        }

#if !WINDOWS_UWP && !WIN_UI
        bool isDeploymentDone = Deploy(tests, runContext, frameworkHandle);
#else
        const bool isDeploymentDone = false;
#endif

        // Placing this after deployment since we need information post deployment that we pass in as properties.
        CacheSessionParameters(runContext?.RunSettings?.SettingsXml, frameworkHandle.ToAdapterMessageLogger());

        // Run tests.
        await ExecuteTestsAsync(tests, runContext, frameworkHandle, testResultRecorder, filterProvider, isDeploymentDone).ConfigureAwait(false);

#if !WINDOWS_UWP && !WIN_UI
        if (!_hasAnyTestFailed)
        {
            PlatformServiceProvider.Instance.TestDeployment.Cleanup();
        }
#endif
    }

#if !WINDOWS_UWP && !WIN_UI
    /// <summary>
    /// Runs deployment for the given tests via the platform-agnostic deployment service, translating the host
    /// run context into the neutral <see cref="DeploymentContext"/> at this call site. This is the single place
    /// execution reads the VSTest run context for deployment (removed once the run context itself is neutralized).
    /// </summary>
    private static bool Deploy(IEnumerable<UnitTestElement> tests, IRunContext? runContext, IFrameworkHandle frameworkHandle)
    {
        var deploymentContext = new DeploymentContext(runContext?.TestRunDirectory, runContext?.RunSettings?.SettingsXml);
        return PlatformServiceProvider.Instance.TestDeployment.Deploy(tests, deploymentContext, frameworkHandle.ToAdapterMessageLogger());
    }
#endif

    internal virtual UnitTestDiscoverer GetUnitTestDiscoverer(ITestSourceHandler testSourceHandler) => new(testSourceHandler);
}
