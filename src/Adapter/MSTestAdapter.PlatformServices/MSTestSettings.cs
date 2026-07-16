// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using DebuggerLaunchMode = Microsoft.VisualStudio.TestTools.UnitTesting.DebuggerLaunchMode;
using ExecutionScope = Microsoft.VisualStudio.TestTools.UnitTesting.ExecutionScope;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

/// <summary>
/// Adapter Settings for the run.
/// </summary>
#if NETFRAMEWORK
[Serializable]
#endif
internal sealed partial class MSTestSettings
{
    /// <summary>
    /// The settings name.
    /// </summary>
    public const string SettingsName = "MSTest";

    /// <summary>
    /// The alias to the default settings name.
    /// </summary>
    public const string SettingsNameAlias = "MSTestV2";

    private const string ParallelizeSettingsName = "Parallelize";

    /// <summary>
    /// Initializes a new instance of the <see cref="MSTestSettings"/> class.
    /// </summary>
    public MSTestSettings()
    {
        OutputCaptureMode = TestOutputCaptureMode.Result;
        MapInconclusiveToFailed = false;
        MapNotRunnableToFailed = true;
        TreatDiscoveryWarningsAsErrors = true;
        DisableParallelization = false;
        ConsiderEmptyDataSourceAsInconclusive = false;
        TestTimeout = 0;
        AssemblyInitializeTimeout = 0;
        ClassInitializeTimeout = 0;
        AssemblyCleanupTimeout = 0;
        ClassCleanupTimeout = 0;
        TestInitializeTimeout = 0;
        TestCleanupTimeout = 0;
        GlobalTestInitializeTimeout = 0;
        GlobalTestCleanupTimeout = 0;
        CooperativeCancellationTimeout = false;
        OrderTestsByNameInClass = false;
        RandomizeTestOrder = false;
        RandomTestOrderSeed = null;
        LaunchDebuggerOnAssertionFailure = DebuggerLaunchMode.Disabled;
    }

    /// <summary>
    /// Gets the current settings.
    /// </summary>
    [field: AllowNull]
    [field: MaybeNull]
    [AllowNull]
    public static MSTestSettings CurrentSettings
    {
        get => field ??= new MSTestSettings();
        private set;
    }

    /// <summary>
    /// Gets the current configuration settings.
    /// </summary>
    [field: AllowNull]
    [field: MaybeNull]
    [AllowNull]
    public static RunConfigurationSettings RunConfigurationSettings
    {
        get => field ??= new RunConfigurationSettings();
        private set;
    }

    /// <summary>
    /// Gets a value indicating whether capture debug traces.
    /// </summary>
    public bool CaptureDebugTraces => OutputCaptureMode != TestOutputCaptureMode.None;

    /// <summary>
    /// Gets a value indicating how Console/Debug/Trace output produced during test execution is handled.
    /// </summary>
    internal TestOutputCaptureMode OutputCaptureMode { get; private set; }

    /// <summary>
    /// Gets a value indicating whether an inconclusive result be mapped to failed test.
    /// </summary>
    public bool MapInconclusiveToFailed { get; private set; }

    /// <summary>
    /// Gets a value indicating whether a not runnable result be mapped to failed test.
    /// </summary>
    public bool MapNotRunnableToFailed { get; private set; }

    /// <summary>
    /// Gets a value indicating whether or not test discovery warnings should be treated as errors.
    /// </summary>
    public bool TreatDiscoveryWarningsAsErrors { get; private set; }

    /// <summary>
    /// Gets the number of threads/workers to be used for parallelization.
    /// </summary>
    public int? ParallelizationWorkers { get; private set; }

    /// <summary>
    /// Gets the scope of parallelization.
    /// </summary>
    public ExecutionScope? ParallelizationScope { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the assembly can be parallelized.
    /// </summary>
    /// <remarks>
    /// This is also re-used to disable parallelization on format errors.
    /// </remarks>
    public bool DisableParallelization { get; private set; }

    /// <summary>
    ///  Gets specified global test case timeout.
    /// </summary>
    public int TestTimeout { get; private set; }

    /// <summary>
    ///  Gets specified global AssemblyInitialize timeout.
    /// </summary>
    internal int AssemblyInitializeTimeout { get; private set; }

    /// <summary>
    ///  Gets specified global AssemblyCleanup timeout.
    /// </summary>
    internal int AssemblyCleanupTimeout { get; private set; }

    /// <summary>
    ///  Gets a value indicating whether to enable marking tests with missing dynamic data as Inconclusive.
    /// </summary>
    internal bool ConsiderEmptyDataSourceAsInconclusive { get; private set; }

    /// <summary>
    ///  Gets specified global ClassInitializeTimeout timeout.
    /// </summary>
    internal int ClassInitializeTimeout { get; private set; }

    /// <summary>
    ///  Gets specified global ClassCleanupTimeout timeout.
    /// </summary>
    internal int ClassCleanupTimeout { get; private set; }

    /// <summary>
    ///  Gets specified TestInitialize timeout.
    /// </summary>
    internal int TestInitializeTimeout { get; private set; }

    /// <summary>
    ///  Gets specified TestCleanup timeout.
    /// </summary>
    internal int TestCleanupTimeout { get; private set; }

    /// <summary>
    ///  Gets specified GlobalTestInitialize timeout. Falls back to <see cref="TestInitializeTimeout"/> when not set.
    /// </summary>
    internal int GlobalTestInitializeTimeout { get; private set; }

    /// <summary>
    ///  Gets specified GlobalTestCleanup timeout. Falls back to <see cref="TestCleanupTimeout"/> when not set.
    /// </summary>
    internal int GlobalTestCleanupTimeout { get; private set; }

    /// <summary>
    /// Gets a value indicating whether all timeouts should be cooperative.
    /// </summary>
    internal bool CooperativeCancellationTimeout { get; private set; }

    /// <summary>
    /// Gets a value indicating whether tests should be ordered by name in the class.
    /// </summary>
    internal bool OrderTestsByNameInClass { get; private set; }

    /// <summary>
    /// Gets a value indicating whether tests should be executed in a random order to help expose hidden dependencies between tests.
    /// </summary>
    /// <remarks>
    /// When enabled, the adapter shuffles assemblies, parallel chunks and individual tests using <see cref="RandomTestOrderSeed"/>
    /// (or an auto-generated seed). Tests marked with <c>[DoNotParallelize]</c> are still executed after the parallelizable set,
    /// but their relative order is also randomized. When this option is enabled, <see cref="OrderTestsByNameInClass"/> is ignored.
    /// </remarks>
    internal bool RandomizeTestOrder { get; private set; }

    /// <summary>
    /// Gets the seed used to randomize test execution order when <see cref="RandomizeTestOrder"/> is enabled.
    /// </summary>
    /// <remarks>
    /// When <see langword="null"/>, the adapter generates a seed at run time and reports it in the run output so that the same
    /// order can be reproduced by setting this value to the reported seed.
    /// </remarks>
    internal int? RandomTestOrderSeed { get; private set; }

    /// <summary>
    /// Gets a value specifying when to launch the debugger on assertion failure.
    /// </summary>
    internal DebuggerLaunchMode LaunchDebuggerOnAssertionFailure { get; private set; }

    /// <summary>
    /// Resets any settings loaded.
    /// </summary>
    internal static void Reset()
    {
        CurrentSettings = null;
        RunConfigurationSettings = null;
    }
}
