// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
using System.Security;
#endif

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.Logging;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

/// <summary>
/// The runner that runs a single unit test. Also manages the assembly and class cleanup methods at the end of the run.
/// </summary>
[StackTraceHidden]
internal sealed partial class UnitTestRunner
#if NETFRAMEWORK
    : MarshalByRefObject
#endif
{
    // Reusable TestResult returned for the assembly-init fast path (init already passed).
    // Safe to share across concurrent test runs because it is private to this type and treated
    // as immutable: the fast path only reads from it (Outcome plus the null Log/DebugTrace fields)
    // and the instance never escapes RunSingleTestAsync. Do not mutate it.
    private static readonly TestResult AssemblyInitPassedResult = new() { Outcome = UnitTestOutcome.Passed };

    private readonly TypeCache _typeCache;
    private readonly ClassCleanupManager _classCleanupManager;

    // Only needed to attach class cleanup failures to the right test.
    // So we only add to this dictionary if the class has a class cleanup.
    private readonly ConcurrentDictionary<string, UnitTestElement> _lastRunnableTestByClass = new();

    // Used to attach assembly cleanup failures to the right test.
    private UnitTestElement? _lastRunnableTestInWholeAssembly;

    // Tracks whether at least one test in this runner's lifetime triggered AssemblyInitialize.
    // Needed so that end-of-assembly cleanup still runs when the very last test of the assembly
    // was filtered out by a [TestFilterProvider] (in which case testMethodInfo is null for the
    // cleanup decision in RunSingleTestAsync).
    private bool _assemblyInitializeWasExecuted;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnitTestRunner"/> class.
    /// </summary>
    /// <param name="settings"> Specifies adapter settings that need to be instantiated in the domain running these tests. </param>
    /// <param name="testsToRun"> The tests to run. </param>
    public UnitTestRunner(MSTestSettings settings, UnitTestElement[] testsToRun)
        : this(settings, testsToRun, ReflectHelper.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnitTestRunner"/> class.
    /// </summary>
    /// <param name="settings"> Specifies adapter settings. </param>
    /// <param name="testsToRun"> The tests to run. </param>
    /// <param name="reflectHelper"> The reflect Helper. </param>
    internal UnitTestRunner(MSTestSettings settings, UnitTestElement[] testsToRun, ReflectHelper reflectHelper)
    {
        // Populate the settings into the domain(Desktop workflow) performing discovery.
        // This would just be resetting the settings to itself in non desktop workflows.
        MSTestSettings.PopulateSettings(settings);

        // Bridge the adapter setting to the TestFramework for assertion failure behavior.
        AssertionFailureSettings.LaunchDebuggerOnAssertionFailure = MSTestSettings.CurrentSettings.LaunchDebuggerOnAssertionFailure;

        Logger.OnLogMessage += message => (TestContext.Current as TestContextImplementation)?.WriteConsoleOut(message);

        ConfigureOutputRouting(MSTestSettings.CurrentSettings.OutputCaptureMode);

        PlatformServiceProvider.Instance.TestRunCancellationToken ??= new TestRunCancellationToken();
        _typeCache = new TypeCache(reflectHelper);

        _classCleanupManager = new ClassCleanupManager(testsToRun);

        // Expose the planned (post-filter) test list so that user code (typically [AssemblyInitialize]
        // or fixtures) can query TestRun.Current.PlannedTests to decide whether expensive setup is
        // needed. Set here so the snapshot lives in the same AppDomain/process that will execute the
        // assembly initialize and the tests themselves.
        TestRun.SetCurrent(TestRunInfo.CreateFrom(testsToRun));
    }

    // Console.SetOut/SetError replace the process-wide console, and Trace.Listeners is process-wide too.
    // We install our routing exactly once per process: re-wrapping on every runner would stack routers on
    // top of each other (and Console.Out returns a synchronized wrapper, not our router, so a type check
    // cannot reliably detect an already-installed router). Installing once guarantees a single capture hop
    // per write and makes the live echo target the real console rather than a previously installed router.
    private static int s_outputRoutingInstalled;

    private static void ConfigureOutputRouting(TestOutputCaptureMode mode)
    {
        // None means "do not capture": leave Console/Trace untouched so output flows to its normal
        // destination, matching the legacy CaptureTraceOutput=false behavior. Within a single test session
        // the capture mode is constant, so we do not tear down routing a previous run may have installed
        // (restoring the process-wide console mid-session is not thread-safe).
        if (mode == TestOutputCaptureMode.None)
        {
            return;
        }

        // Install exactly once per process, even if multiple runners are constructed concurrently.
        if (Interlocked.CompareExchange(ref s_outputRoutingInstalled, 1, 0) != 0)
        {
            return;
        }

        bool echoLive = mode == TestOutputCaptureMode.Live;
        TextWriter originalOut = Console.Out;
        TextWriter originalError = Console.Error;
        Console.SetOut(new ConsoleOutRouter(originalOut, echoLive));
        Console.SetError(new ConsoleErrorRouter(originalError, echoLive));
        Trace.Listeners.Add(new TextWriterTraceListener(new TraceTextWriter(echoLive ? originalOut : null)));
    }

#pragma warning disable CA1822 // Mark members as static
    public void Cancel()
        => PlatformServiceProvider.Instance.TestRunCancellationToken?.Cancel();
#pragma warning restore CA1822 // Mark members as static

#if NETFRAMEWORK
    /// <summary>
    /// Returns object to be used for controlling lifetime, null means infinite lifetime.
    /// </summary>
    /// <returns>
    /// The <see cref="object"/>.
    /// </returns>
    [SecurityCritical]
    public override object? InitializeLifetimeService() => null;
#endif
}
