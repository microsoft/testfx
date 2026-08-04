// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
using System.Data;
using System.Data.Common;
#endif

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
    /// <summary>
    /// Properties.
    /// </summary>
    private readonly Dictionary<string, object?> _properties;
    private readonly IAdapterMessageLogger? _messageLogger;
    private readonly TestRunCancellationToken? _testRunCancellationToken;
    private readonly TextWriter? _liveOutputWriter;
    private readonly Func<TestOutputCaptureMode> _outputCaptureModeProvider;

    private CancellationTokenRegistration? _cancellationTokenRegistration;

    /// <summary>
    /// Unit test outcome.
    /// </summary>
    private UnitTestOutcome _outcome;

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

    /// <summary>
    /// Gets the inner test context object.
    /// </summary>
    public TestContext Context => this;

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
