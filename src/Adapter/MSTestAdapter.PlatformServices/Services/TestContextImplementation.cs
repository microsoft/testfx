// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
using System.Data;
using System.Data.Common;
#endif

using System.Collections.ObjectModel;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using ITestMethod = Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ObjectModel.ITestMethod;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

/// <summary>
/// Internal implementation of TestContext exposed to the user.
/// The virtual string properties of the TestContext are retrieved from the property dictionary
/// like GetProperty&lt;string&gt;("TestName") or GetProperty&lt;string&gt;("FullyQualifiedTestClassName").
/// </summary>
internal sealed partial class TestContextImplementation : TestContext, ITestContext, IDisposable
{
#if !WINDOWS_UWP && !WIN_UI
    /// <summary>
    /// Environment variable that, when set to a truthy value, retains every per-test temporary
    /// directory (including those of passing tests) instead of deleting them. Used for debugging.
    /// </summary>
    private const string RetainTestTempDirectoryEnvironmentVariable = "MSTEST_TEST_TEMP_DIRECTORY_RETAIN";

    /// <summary>
    /// Maximum length (in characters) of the readable, sanitized test-name portion of the
    /// per-test temporary directory name. This is a <em>cap</em>: the actual budget is computed
    /// adaptively from how much room the base path leaves (see <see cref="CreateTestTempDirectory"/>),
    /// and is only allowed to grow up to this cap.
    /// </summary>
    private const int TestTempDirectoryNameMaxLength = 50;

    /// <summary>
    /// Minimum readable-name budget worth keeping. If the base path is so deep that the adaptive
    /// budget for the readable portion would drop below this floor, the implementation falls back to
    /// the system temporary directory (which is short) rather than emit a barely-readable name that
    /// still risks overflowing <c>MAX_PATH</c>.
    /// </summary>
    private const int TestTempDirectoryNameMinLength = 8;

    /// <summary>
    /// Number of hexadecimal characters of the uniqueness suffix appended to the directory name.
    /// This is a full 128-bit GUID (32 hex chars, <c>Guid.ToString("N")</c>) so that two contexts
    /// choosing the same suffix is cryptographically negligible even at very large scales — the
    /// <see cref="Directory.Exists"/> pre-check plus <c>Directory.CreateDirectory</c> is not an
    /// atomic exclusive create, so uniqueness must come from the entropy of the suffix rather than
    /// from the check.
    /// </summary>
    private const int TestTempDirectoryUniqueSuffixLength = 32;

    /// <summary>
    /// Characters that are reserved in a Windows file name. Sanitization strips these on every OS
    /// (not just via <see cref="Path.GetInvalidFileNameChars"/>, which on Unix returns only
    /// <c>/</c> and NUL) so the generated directory name is portable and never accidentally embeds
    /// a path separator or wildcard when the run is later inspected on a different platform.
    /// </summary>
    private const string WindowsReservedFileNameChars = "<>:\"/\\|?*";

    /// <summary>
    /// The Windows <c>MAX_PATH</c> limit. The feature targets this classic 260-character limit and
    /// deliberately does not rely on long-path opt-in (<c>LongPathsEnabled</c> / <c>\\?\</c>), which
    /// is not guaranteed to be enabled and is frequently not honored by external tools that E2E
    /// tests shell out to.
    /// </summary>
    private const int WindowsMaxPath = 260;

    /// <summary>
    /// Characters reserved <em>inside</em> the per-test temporary directory for the files the test
    /// itself writes (e.g. <c>subdir\result.json</c>). The adaptive budget guarantees at least this
    /// much headroom under <c>MAX_PATH</c> on Windows, so a test's own writes do not fail with a
    /// baffling <see cref="System.IO.PathTooLongException"/> originating in user code.
    /// </summary>
    private const int TestTempDirectoryReservedHeadroom = 80;
#endif

    /// <summary>
    /// Properties.
    /// </summary>
    private readonly Dictionary<string, object?> _properties;
#if NET9_0_OR_GREATER
    private readonly Lock _propertiesLock = new();
#else
    private readonly object _propertiesLock = new();
#endif
    private readonly IAdapterMessageLogger? _messageLogger;
    private readonly TestRunCancellationToken? _testRunCancellationToken;
    private readonly TextWriter? _liveOutputWriter;
    private readonly Func<TestOutputCaptureMode> _outputCaptureModeProvider;

#if !WINDOWS_UWP && !WIN_UI
    /// <summary>
    /// Guards lazy creation of <see cref="_testTempDirectory"/>.
    /// </summary>
#if NET9_0_OR_GREATER
    private readonly Lock _testTempDirectoryLock = new();
#else
    private readonly object _testTempDirectoryLock = new();
#endif
#endif

    private CancellationTokenRegistration? _cancellationTokenRegistration;

    /// <summary>
    /// List of result files associated with the test.
    /// </summary>
    private List<string>? _testResultFiles;

    private SynchronizedStringBuilder? _stdOutStringBuilder;
    private SynchronizedStringBuilder? _stdErrStringBuilder;
    private SynchronizedStringBuilder? _traceStringBuilder;
    private SynchronizedStringBuilder? _testContextMessageStringBuilder;

    /// <summary>
    /// Unit test outcome.
    /// </summary>
    private UnitTestOutcome _outcome;

#if !WINDOWS_UWP && !WIN_UI
    /// <summary>
    /// Whether this context represents an executing test (as opposed to an assembly/class
    /// initialize or cleanup fixture context). <see cref="TestTempDirectory"/> is a *per-test*
    /// scratch directory; fixture contexts are not per-test and are not always disposed (e.g.
    /// <c>ClassCleanupManager.ForceCleanup</c> contexts), so creating a directory for them would
    /// leak it. The getter returns <see langword="null"/> when this is <see langword="false"/>.
    /// </summary>
    private bool _isTestExecutionContext;

    /// <summary>
    /// The lazily-created per-test temporary directory, or <see langword="null"/> if it has not
    /// been accessed (and therefore not created) yet.
    /// </summary>
    private string? _testTempDirectory;

    /// <summary>
    /// Whether <see cref="_testTempDirectory"/> has been created.
    /// </summary>
    private bool _testTempDirectoryCreated;

    /// <summary>
    /// Whether cleanup of the per-test temporary directory has started (i.e. the context is being
    /// or has been disposed). Once set, the getter must not create a new directory, otherwise a
    /// late access from a background thread the test spawned could create a directory *after*
    /// cleanup already ran, leaking it. Guarded by <see cref="_testTempDirectoryLock"/>.
    /// </summary>
    private bool _testTempDirectoryCleanupStarted;
#endif

#if NETFRAMEWORK
    /// <summary>
    /// DB connection for test context.
    /// </summary>
    private DbConnection? _dbConnection;

    /// <summary>
    /// Data row for TestContext.
    /// </summary>
    private DataRow? _dataRow;
#endif

    private static readonly Action<object?> CancelDelegate = static state => ((TestContextImplementation)state!).Context.CancellationTokenSource.Cancel();

    /// <summary>
    /// Initializes a new instance of the <see cref="TestContextImplementation"/> class.
    /// </summary>
    /// <param name="testMethod">The test method.</param>
    /// <param name="testClassFullName">The test class full name.</param>
    /// <param name="properties">Properties/configuration passed in.</param>
    /// <param name="messageLogger">The message logger to use.</param>
    /// <param name="testRunCancellationToken">The global test run cancellation token.</param>
    internal TestContextImplementation(ITestMethod? testMethod, string? testClassFullName, IDictionary<string, object?> properties, IAdapterMessageLogger? messageLogger, TestRunCancellationToken? testRunCancellationToken)
        : this(
            testMethod,
            testClassFullName,
            properties,
            messageLogger,
            testRunCancellationToken,
            Volatile.Read(ref s_liveOutputWriter),
            static () => MSTestSettings.CurrentSettings.OutputCaptureMode)
    {
    }

    private TestContextImplementation(
        ITestMethod? testMethod,
        string? testClassFullName,
        IDictionary<string, object?> properties,
        IAdapterMessageLogger? messageLogger,
        TestRunCancellationToken? testRunCancellationToken,
        TextWriter? liveOutputWriter,
        Func<TestOutputCaptureMode> outputCaptureModeProvider)
    {
        // testMethod can be null when running ForceCleanup (done when reaching --maximum-failed-tests.
        DebugEx.Assert(properties != null, "properties is not null");

        testClassFullName ??= testMethod?.FullClassName;
        if (testClassFullName is null && testMethod is null)
        {
            _properties = [with(properties)];
        }
        else
        {
            _properties = [with(properties.Count + 2)];
            foreach (KeyValuePair<string, object?> kvp in properties)
            {
                _properties[kvp.Key] = kvp.Value;
            }

            if (testClassFullName is not null)
            {
                // Use indexer assignment instead of Add so that re-seeding from a parent property
                // snapshot that may already contain this key does not throw.
                _properties[FullyQualifiedTestClassNameLabel] = testClassFullName;
            }

            if (testMethod is not null)
            {
                // Use indexer assignment instead of Add for the same reason.
                _properties[TestNameLabel] = testMethod.Name;
            }
        }

        _messageLogger = messageLogger;
        _testRunCancellationToken = testRunCancellationToken;
        _liveOutputWriter = liveOutputWriter;
        _outputCaptureModeProvider = outputCaptureModeProvider;
        _cancellationTokenRegistration = testRunCancellationToken?.Register(CancelDelegate, this);
#if !WINDOWS_UWP && !WIN_UI
        // A non-null testMethod means this context is created for an executing test. Fixture
        // (assembly/class initialize and cleanup) contexts pass testMethod: null; data-driven
        // iteration clones re-enable this flag explicitly (see CloneForDataDrivenIteration).
        _isTestExecutionContext = testMethod is not null;
#endif
    }

    #region TestContext impl

    /// <inheritdoc/>
    public override UnitTestOutcome CurrentTestOutcome => _outcome;

#if NETFRAMEWORK
    /// <inheritdoc/>
    public override DbConnection? DataConnection => _dbConnection;

    /// <inheritdoc/>
    public override DataRow? DataRow => _dataRow;
#endif

    /// <inheritdoc/>
    public override IDictionary<string, object?> Properties => _properties;

#if !WINDOWS_UWP && !WIN_UI
    /// <inheritdoc/>
    public override string? TestTempDirectory
    {
        get
        {
            // A per-test scratch directory only makes sense for an executing test. Fixture
            // (assembly/class initialize and cleanup) contexts are not per-test and may never be
            // disposed, so creating a directory for them would leak it — return null instead.
            if (!_isTestExecutionContext)
            {
                return null;
            }

            if (Volatile.Read(ref _testTempDirectoryCreated))
            {
                return _testTempDirectory;
            }

            lock (_testTempDirectoryLock)
            {
                if (!_testTempDirectoryCreated)
                {
                    if (_testTempDirectoryCleanupStarted)
                    {
                        // The context is being (or has been) disposed. Creating a directory now
                        // would leak it, because cleanup has already inspected the state. Treat a
                        // post-cleanup access as a no-op and return null (the test has finished).
                        return null;
                    }

                    _testTempDirectory = CreateTestTempDirectory();
                    Volatile.Write(ref _testTempDirectoryCreated, true);
                }
            }

            return _testTempDirectory;
        }
    }
#endif

    /// <summary>
    /// Gets the inner test context object.
    /// </summary>
    public TestContext Context => this;

    /// <inheritdoc/>
    public override void AddResultFile(string fileName)
    {
        if (StringEx.IsNullOrEmpty(fileName))
        {
            throw new ArgumentException(Resource.Common_CannotBeNullOrEmpty, nameof(fileName));
        }

        (_testResultFiles ??= []).Add(Path.GetFullPath(fileName));
    }

    /// <summary>
    /// When overridden in a derived class, used to write trace messages while the
    ///     test is running.
    /// </summary>
    /// <param name="message">The formatted string that contains the trace message.</param>
    public override void Write(string? message)
    {
        string? msg = message?.Replace("\0", "\\0");
        TestContextMessageBuilder.Append(msg);
        WriteLive(msg, appendLine: false);
    }

    /// <summary>
    /// When overridden in a derived class, used to write trace messages while the
    ///     test is running.
    /// </summary>
    /// <param name="format">The string that contains the trace message.</param>
    /// <param name="args">Arguments to add to the trace message.</param>
    public override void Write(string format, params object?[] args)
    {
        string message = string.Format(CultureInfo.CurrentCulture, format.Replace("\0", "\\0"), args);
        TestContextMessageBuilder.Append(message);
        WriteLive(message, appendLine: false);
    }

    /// <summary>
    /// When overridden in a derived class, used to write trace messages while the
    ///     test is running.
    /// </summary>
    /// <param name="message">The formatted string that contains the trace message.</param>
    public override void WriteLine(string? message)
    {
        string? msg = message?.Replace("\0", "\\0");
        TestContextMessageBuilder.AppendLine(msg);
        WriteLive(msg, appendLine: true);
    }

    /// <summary>
    /// When overridden in a derived class, used to write trace messages while the
    ///     test is running.
    /// </summary>
    /// <param name="format">The string that contains the trace message.</param>
    /// <param name="args">Arguments to add to the trace message.</param>
    public override void WriteLine(string format, params object?[] args)
    {
        string message = string.Format(CultureInfo.CurrentCulture, format.Replace("\0", "\\0"), args);
        TestContextMessageBuilder.AppendLine(message);
        WriteLive(message, appendLine: true);
    }

    /// <summary>
    /// Set the unit-test outcome.
    /// </summary>
    /// <param name="outcome">The test outcome.</param>
    public void SetOutcome(UnitTestOutcome outcome)
        => _outcome = outcome;

    /// <inheritdoc/>
    public void SetException(Exception? exception)
        => TestException = exception;

    /// <summary>
    /// Set data row for particular run of TestMethod.
    /// </summary>
    /// <param name="dataRow">data row.</param>
    public void SetDataRow(object? dataRow)
    {
#if NETFRAMEWORK
#pragma warning disable IDE0022 // Use expression body for method
        _dataRow = dataRow as DataRow;
#pragma warning restore IDE0022 // Use expression body for method
#endif
    }

    /// <inheritdoc/>
    public void SetTestData(object?[]? data) => TestData = data;

    /// <summary>
    /// Set connection for TestContext.
    /// </summary>
    /// <param name="dbConnection">db Connection.</param>
    public void SetDataConnection(object? dbConnection)
    {
#if NETFRAMEWORK
#pragma warning disable IDE0022 // Use expression body for method
        _dbConnection = dbConnection as DbConnection;
#pragma warning restore IDE0022 // Use expression body for method
#endif
    }

    /// <summary>
    /// Returns whether property with parameter name is present or not.
    /// </summary>
    /// <param name="propertyName">The property name.</param>
    /// <param name="propertyValue">The property value.</param>
    /// <returns>True if found.</returns>
    public bool TryGetPropertyValue(string propertyName, out object? propertyValue)
        => _properties.TryGetValue(propertyName, out propertyValue);

    /// <summary>
    /// Adds the parameter name/value pair to property bag.
    /// </summary>
    /// <param name="propertyName">The property name.</param>
    /// <param name="propertyValue">The property value.</param>
    public void AddProperty(string propertyName, string propertyValue)
        => _properties.Add(propertyName, propertyValue);

    /// <summary>
    /// Merges the given properties into this context's property bag using indexer semantics
    /// (existing keys are overwritten, except the per-context labels
    /// <see cref="TestContext.FullyQualifiedTestClassNameLabel"/> and
    /// <see cref="TestContext.TestNameLabel"/>, which are preserved).
    /// Used to flow properties set during <c>AssemblyInitialize</c> / <c>ClassInitialize</c>
    /// into subsequent contexts.
    /// <para>
    /// Merge precedence: keys in <paramref name="propertiesToMerge"/> WIN over keys already
    /// present in this context's bag. This is intentional — lifecycle snapshots typically
    /// flow on top of the seeded source-level parameters (e.g. <c>TestRunParameters</c> from
    /// <c>.runsettings</c>), so a user's explicit assignment in <c>AssemblyInitialize</c> /
    /// <c>ClassInitialize</c> overrides any same-named runsettings value for the rest of
    /// the lifecycle (class init, tests, class cleanup, assembly cleanup).
    /// </para>
    /// </summary>
    /// <param name="propertiesToMerge">The properties to merge in. May be <see langword="null"/>.</param>
    internal void MergeProperties(IReadOnlyDictionary<string, object?>? propertiesToMerge)
    {
        if (propertiesToMerge is null or { Count: 0 })
        {
            return;
        }

        // Take the same internal lock as CaptureLifecycleProperties so a snapshot capture
        // cannot race with a merge on the same context (which would otherwise corrupt the
        // Dictionary iterator or cause a missed write). Writes via the public Properties
        // indexer still bypass this lock - see the remarks on CaptureLifecycleProperties.
        lock (_propertiesLock)
        {
            foreach (KeyValuePair<string, object?> kvp in propertiesToMerge)
            {
                // Never overwrite the per-context labels.
                if (kvp.Key == FullyQualifiedTestClassNameLabel || kvp.Key == TestNameLabel)
                {
                    continue;
                }

                _properties[kvp.Key] = kvp.Value;
            }
        }
    }

    /// <summary>
    /// Captures a snapshot of the current property bag, excluding the per-context labels
    /// (<see cref="TestContext.FullyQualifiedTestClassNameLabel"/> and
    /// <see cref="TestContext.TestNameLabel"/>). The returned dictionary is intended to be
    /// stored on a <c>TestAssemblyInfo</c> / <c>TestClassInfo</c> and later merged into other
    /// contexts via <see cref="MergeProperties(IReadOnlyDictionary{string, object?}?)"/>.
    /// <para>
    /// Returns <see langword="null"/> when there are no non-label properties to capture
    /// (the common case when <c>AssemblyInitialize</c> / <c>ClassInitialize</c> do not set
    /// properties on <c>TestContext</c>). <see cref="MergeProperties"/> already handles a
    /// <see langword="null"/> argument as a no-op, so callers need not special-case this.
    /// </para>
    /// <para>
    /// The snapshot is shallow: keys and value references are copied as-is. Reference-type
    /// values stored in the bag (e.g. a mocked file system, a connection pool, a list) are
    /// shared across every context the snapshot is later merged into. Mutations of those
    /// reference-type instances are visible everywhere.
    /// </para>
    /// <para>
    /// Enumeration is performed under a private synchronization lock so that snapshot
    /// capture is safe against concurrent calls to this method or <see cref="MergeProperties"/>
    /// on the same context. Note: writes made via the public <see cref="Properties"/> indexer
    /// do NOT take this lock, so a lifecycle method that spawns a background thread which
    /// keeps mutating <see cref="Properties"/> past method return can still race with the
    /// capture - that is treated as user error and is consistent with the pre-existing
    /// thread-affinity expectation of <c>AssemblyInitialize</c> / <c>ClassInitialize</c>.
    /// </para>
    /// </summary>
    /// <returns>
    /// A read-only snapshot of the current properties (excluding per-context labels), or
    /// <see langword="null"/> if there are no such properties to snapshot.
    /// </returns>
    internal IReadOnlyDictionary<string, object?>? CaptureLifecycleProperties()
    {
        Dictionary<string, object?>? snapshot = null;
        lock (_propertiesLock)
        {
            foreach (KeyValuePair<string, object?> kvp in _properties)
            {
                if (kvp.Key == FullyQualifiedTestClassNameLabel || kvp.Key == TestNameLabel)
                {
                    continue;
                }

#pragma warning disable IDE0028 // Collection initialization can be simplified - capacity hint is intentional.
                snapshot ??= new Dictionary<string, object?>(_properties.Count);
#pragma warning restore IDE0028
                snapshot[kvp.Key] = kvp.Value;
            }
        }

        return snapshot is null ? null : new ReadOnlyDictionary<string, object?>(snapshot);
    }

    /// <summary>
    /// Result files attached.
    /// </summary>
    /// <returns>Results files generated in run.</returns>
    public IList<string>? GetResultFiles()
    {
        if (_testResultFiles is null || _testResultFiles.Count == 0)
        {
            return null;
        }

        // Hand over the existing list to the caller (callers only enumerate it) and reset the field
        // so data driven tests start with a fresh list on the next AddResultFile call.
        // This avoids the copy that ToList() would do.
        List<string> results = _testResultFiles;
        _testResultFiles = null;

        return results;
    }

    /// <summary>
    /// Gets messages from the testContext writeLines.
    /// </summary>
    /// <returns>The test context messages added so far.</returns>
    public string? GetDiagnosticMessages()
        => _testContextMessageStringBuilder?.ToString();

    /// <summary>
    /// Clears the previous testContext writeline messages.
    /// </summary>
    public void ClearDiagnosticMessages()
        => _testContextMessageStringBuilder?.Clear();

    /// <inheritdoc/>
    public void SetDisplayName(string? displayName)
        => TestDisplayName = displayName;

    /// <inheritdoc/>
    public override void DisplayMessage(MessageLevel messageLevel, string message)
        => _messageLogger?.SendMessage(messageLevel, message);
    #endregion

    /// <inheritdoc/>
    public void Dispose()
    {
        _cancellationTokenRegistration?.Dispose();
        _cancellationTokenRegistration = null;
#if !WINDOWS_UWP && !WIN_UI
        CleanupTestTempDirectory();
#endif
    }

#if !WINDOWS_UWP && !WIN_UI
    /// <summary>
    /// Creates the per-test temporary directory. The directory lives under the run's results
    /// directory (so it is discoverable next to other run output); when no results directory is
    /// configured this is the test assembly's output directory. On Windows the readable-name budget
    /// is sized adaptively from how much room the base path leaves under <c>MAX_PATH</c>, and — when
    /// the results directory is so deep that even a minimal readable name cannot preserve the
    /// reserved headroom for the test's own files — the implementation falls back to the short
    /// system temporary directory instead. It also falls back to the system temporary directory when
    /// the chosen base directory cannot be written to (for example a read-only output directory), so
    /// the property returns a usable path rather than throwing from the getter.
    /// </summary>
    private string CreateTestTempDirectory()
    {
        string? resultsDirectory = TestResultsDirectory is { Length: > 0 } results ? results : null;
        string baseDirectory = resultsDirectory ?? Path.GetTempPath();
        bool baseIsTemp = resultsDirectory is null;

        // Size the readable-name budget so that base + '\' + name + '_' + suffix, plus the reserved
        // headroom for the files the test writes inside, stays within MAX_PATH on Windows. On other
        // operating systems path length is effectively a non-issue (per-component limit is 255 and
        // our whole segment is well under that), so the readable name simply gets the full cap.
        int nameBudget = TestTempDirectoryNameMaxLength;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            int available = ComputeReadableNameBudget(baseDirectory);
            if (available < TestTempDirectoryNameMinLength && !baseIsTemp)
            {
                // The results directory is too deep to leave usable headroom; fall back to the
                // short system temp directory so the test still gets room to write.
                baseDirectory = Path.GetTempPath();
                baseIsTemp = true;
                available = ComputeReadableNameBudget(baseDirectory);
            }

            nameBudget = available < 0 ? 0 : Math.Min(available, TestTempDirectoryNameMaxLength);
        }

        if (TryCreateTestTempDirectoryUnder(baseDirectory, nameBudget, out string created))
        {
            return created;
        }

        // The chosen base directory could not be written to (e.g. a read-only output directory).
        // Fall back to the system temporary directory, which is writable, so the property still
        // returns a usable path instead of throwing from the getter.
        if (!baseIsTemp)
        {
            string tempBase = Path.GetTempPath();
            int tempBudget = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? Math.Max(0, Math.Min(ComputeReadableNameBudget(tempBase), TestTempDirectoryNameMaxLength))
                : TestTempDirectoryNameMaxLength;
            if (TryCreateTestTempDirectoryUnder(tempBase, tempBudget, out created))
            {
                return created;
            }
        }

        // Could not create anywhere with a readable name; make one last attempt under the system
        // temp directory with a plain Guid name and let any exception surface as a genuine error.
        string fallback = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fallback);
        return fallback;
    }

    /// <summary>
    /// Attempts to create a uniquely-named per-test temporary directory under <paramref name="baseDirectory"/>.
    /// Returns <see langword="false"/> (rather than throwing) when the base directory cannot be
    /// written to, so the caller can fall back to another location.
    /// </summary>
    private bool TryCreateTestTempDirectoryUnder(string baseDirectory, int nameBudget, out string createdPath)
    {
        string namePart = SanitizeTestTempDirectoryName(GetTestTempDirectoryNameSource(), nameBudget);

        // The suffix is a full 128-bit GUID, so two contexts choosing the same directory name is
        // cryptographically negligible. Exists + CreateDirectory is not an atomic exclusive create,
        // so the retry loop below is a belt-and-braces guard rather than a real necessity.
        const int maxAttempts = 10;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            string suffix = Guid.NewGuid().ToString("N").Substring(0, TestTempDirectoryUniqueSuffixLength);
            string candidateName = namePart.Length == 0 ? suffix : $"{namePart}_{suffix}";
            string candidate = Path.Combine(baseDirectory, candidateName);
            if (Directory.Exists(candidate))
            {
                continue;
            }

            try
            {
                Directory.CreateDirectory(candidate);
                createdPath = candidate;
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                // The base directory is not writable — a read-only output directory, or (on .NET
                // Framework) a denied filesystem permission surfaced as SecurityException. Signal
                // failure so the caller can fall back to the system temporary directory. Retrying a
                // different name under the same base would not help, so bail out immediately.
                createdPath = string.Empty;
                return false;
            }
        }

        createdPath = string.Empty;
        return false;
    }

    /// <summary>
    /// Computes how many characters the readable portion of the directory name may use so that the
    /// full path plus the reserved headroom for the test's own files fits within <c>MAX_PATH</c>.
    /// May be negative when the base path alone already exhausts the budget.
    /// </summary>
    private static int ComputeReadableNameBudget(string baseDirectory)
        // full path = base + separator + <name> + '_' + <suffix>; reserve headroom for files inside.
        => WindowsMaxPath
            - TestTempDirectoryReservedHeadroom
            - baseDirectory.Length
            - 1 // directory separator between base and the temp directory name
            - 1 // the '_' between the readable name and the unique suffix
            - TestTempDirectoryUniqueSuffixLength;

    private string? GetTestTempDirectoryNameSource()
        => !StringEx.IsNullOrEmpty(TestDisplayName)
            ? TestDisplayName
            : _properties.TryGetValue(TestNameLabel, out object? testName) && testName is string testNameString
                ? testNameString
                : null;

    /// <summary>
    /// Sanitizes a test name into a safe, bounded path segment: invalid path characters and
    /// whitespace become underscores, runs of underscores collapse, and the result is truncated to
    /// <paramref name="maxLength"/> characters.
    /// </summary>
    private static string SanitizeTestTempDirectoryName(string? name, int maxLength)
    {
        if (maxLength <= 0 || StringEx.IsNullOrEmpty(name))
        {
            return string.Empty;
        }

        char[] invalidChars = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(name.Length);
        bool lastWasUnderscore = false;
        foreach (char c in name)
        {
            if (char.IsWhiteSpace(c) || char.IsControl(c)
                || Array.IndexOf(invalidChars, c) >= 0
                || WindowsReservedFileNameChars.IndexOf(c) >= 0)
            {
                if (!lastWasUnderscore)
                {
                    builder.Append('_');
                    lastWasUnderscore = true;
                }
            }
            else
            {
                builder.Append(c);
                lastWasUnderscore = false;
            }
        }

        string sanitized = builder.ToString().Trim('_');
        if (sanitized.Length > maxLength)
        {
            int cutLength = maxLength;

            // Avoid slicing through the middle of a surrogate pair, which would leave a lone
            // surrogate in the directory name and can produce an invalid path segment.
            if (char.IsHighSurrogate(sanitized[cutLength - 1]))
            {
                cutLength--;
            }

            sanitized = sanitized.Substring(0, cutLength).TrimEnd('_');
        }

        return sanitized;
    }

    /// <summary>
    /// Deletes the per-test temporary directory unless the test failed or retention was requested.
    /// Best-effort: it swallows all exceptions, so a passing test cannot be failed by a cleanup
    /// error. It runs after the test has completed, so it cannot extend the test's own execution or
    /// trip its timeout; note that the delete is synchronous, so exception swallowing is guaranteed
    /// but bounded cleanup time is not (a pathologically stalled filesystem could make disposal
    /// itself slow).
    /// </summary>
    private void CleanupTestTempDirectory()
    {
        // Read the lazy-init state under the same lock the getter writes it under. Without this
        // acquire barrier, a directory created on a worker thread the test spawned (but did not
        // join) could be observed as not-yet-created here, silently skipping cleanup and leaking
        // the directory even on a passing test.
        string? directory;
        lock (_testTempDirectoryLock)
        {
            // Mark cleanup as started while holding the lock so a concurrent first getter that has
            // not yet created the directory will see this and skip creation, instead of creating a
            // directory after cleanup has already run (which would leak it).
            _testTempDirectoryCleanupStarted = true;

            if (!_testTempDirectoryCreated || _testTempDirectory is not { Length: > 0 } createdDirectory)
            {
                return;
            }

            directory = createdDirectory;
        }

        if (ShouldRetainTestTempDirectory())
        {
            return;
        }

        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (Exception ex)
        {
            // A leaked file handle, or a transient antivirus/indexer lock on Windows, can make the
            // delete fail. This must never fail an otherwise passing test, so we swallow it and only
            // surface the failure through the diagnostic trace log. The logging itself is guarded so
            // that a misbehaving trace logger cannot let an exception escape Dispose either.
            try
            {
                PlatformServiceProvider.Instance.AdapterTraceLogger.Warning(
                    "Failed to delete per-test temporary directory '{0}': {1}", directory, ex);
            }
            catch (Exception)
            {
                // Intentionally ignored: cleanup is best-effort and must never throw from Dispose.
            }
        }
    }

    /// <summary>
    /// Determines whether the per-test temporary directory should be kept: retained on any
    /// non-passing outcome, or when retention is forced via the environment variable escape hatch.
    /// </summary>
    private bool ShouldRetainTestTempDirectory()
    {
        // Retain a failed (or otherwise non-passing) test's artifacts for inspection.
        if (_outcome != UnitTestOutcome.Passed)
        {
            return true;
        }

        string? retain;
        try
        {
            retain = Environment.GetEnvironmentVariable(RetainTestTempDirectoryEnvironmentVariable);
        }
        catch (System.Security.SecurityException)
        {
            // Environment access is restricted (possible on .NET Framework). Treat the retention
            // override as unset so cleanup does not throw out of Dispose.
            return false;
        }

        return retain is "1" || string.Equals(retain, "true", StringComparison.OrdinalIgnoreCase);
    }
#endif

    internal SynchronizedStringBuilder StandardOutputBuilder
        => GetOrCreate(ref _stdOutStringBuilder);

    internal SynchronizedStringBuilder StandardErrorBuilder
        => GetOrCreate(ref _stdErrStringBuilder);

    internal SynchronizedStringBuilder TraceBuilder
        => GetOrCreate(ref _traceStringBuilder);

    private SynchronizedStringBuilder TestContextMessageBuilder
        => GetOrCreate(ref _testContextMessageStringBuilder);

    private static SynchronizedStringBuilder GetOrCreate(ref SynchronizedStringBuilder? builder)
        => LazyInitializer.EnsureInitialized(ref builder, static () => new())!;

    internal string? GetAndClearOutput()
        => _stdOutStringBuilder?.GetAndClear();

    internal string? GetAndClearError()
        => _stdErrStringBuilder?.GetAndClear();

    internal string? GetAndClearTrace()
        => _traceStringBuilder?.GetAndClear();

    /// <summary>
    /// Creates a sibling <see cref="TestContextImplementation"/> for use by a single iteration
    /// of the folded data-driven test execution path.
    /// <para>
    /// The clone inherits the same configuration as this context (a shallow snapshot of the
    /// property bag, the message logger, the same test-run cancellation token, and on .NET
    /// Framework the current data connection), but registers its own cancellation callback and
    /// starts with no accumulated per-test state (no captured stdout/stderr/trace,
    /// no diagnostic messages, no result files, no exception, no data row, and the
    /// default <see cref="UnitTestOutcome"/> value rather than the original's current outcome).
    /// This keeps the folded path structurally equivalent to the unfolded path, where each
    /// row gets its own <see cref="TestContextImplementation"/>.
    /// </para>
    /// </summary>
    /// <returns>A fresh context suitable for one folded data-driven iteration.</returns>
    internal TestContextImplementation CloneForDataDrivenIteration()
    {
        // Pass _properties directly and testMethod: null / testClassFullName: null because the
        // relevant labels (including TestNameLabel / FullyQualifiedTestClassNameLabel and anything
        // merged from AssemblyInitialize / ClassInitialize) are already in the property bag. The
        // constructor's null/null branch copies the supplied properties into a fresh dictionary
        // via the [with(properties)] spread, so no intermediate snapshot allocation is needed and
        // isolation is preserved: per-iteration mutations to the clone's property bag won't leak
        // back to this instance nor to subsequent iterations, and mutations to this instance after
        // clone creation won't leak into the clone.
        var clone = new TestContextImplementation(testMethod: null, testClassFullName: null, _properties, _messageLogger, _testRunCancellationToken);

        // Preserve TestRunCount so user code that observes it (e.g. retry-aware tests) sees
        // the same value it would see in the unfolded path. TestRunCount represents the
        // execution-attempt count of this test, not per-row state, so it must flow into
        // each iteration's context.
        clone.Context.TestRunCount = Context.TestRunCount;

#if !WINDOWS_UWP && !WIN_UI
        // A folded data-driven iteration IS an executing test (it is passed testMethod: null only
        // because the identifying labels are already in the property bag), so it must support the
        // per-test temporary directory just like the unfolded path where each row gets its own
        // context constructed with a non-null testMethod.
        clone._isTestExecutionContext = _isTestExecutionContext;
#endif

#if NETFRAMEWORK
        clone.SetDataConnection(_dbConnection);
#endif

        return clone;
    }
}
