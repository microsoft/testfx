// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
using System.Data;
using System.Data.Common;
#endif

using System.Collections.ObjectModel;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using ITestMethod = Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ObjectModel.ITestMethod;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

/// <summary>
/// Internal implementation of TestContext exposed to the user.
/// The virtual string properties of the TestContext are retrieved from the property dictionary
/// like GetProperty&lt;string&gt;("TestName") or GetProperty&lt;string&gt;("FullyQualifiedTestClassName").
/// </summary>
internal sealed class TestContextImplementation : TestContext, ITestContext, IDisposable
{
    internal sealed class SynchronizedStringBuilder
    {
        private readonly StringBuilder _builder = new();

        [MethodImpl(MethodImplOptions.Synchronized)]
        internal void Append(char value)
            => _builder.Append(value);

        [MethodImpl(MethodImplOptions.Synchronized)]
        internal void Append(string? value)
            => _builder.Append(value);

        [MethodImpl(MethodImplOptions.Synchronized)]
        internal void Append(char[] buffer, int index, int count)
            => _builder.Append(buffer, index, count);

        [MethodImpl(MethodImplOptions.Synchronized)]
        internal void AppendLine(string? value)
            => _builder.AppendLine(value);

        [MethodImpl(MethodImplOptions.Synchronized)]
        internal void Clear()
            => _builder.Clear();

        [MethodImpl(MethodImplOptions.Synchronized)]
        internal string GetAndClear()
        {
            string result = _builder.ToString();
            _builder.Clear();

            return result;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public override string ToString()
            => _builder.ToString();
    }

    /// <summary>
    /// Properties.
    /// </summary>
    private readonly Dictionary<string, object?> _properties;
#if NET9_0_OR_GREATER
    private readonly Lock _propertiesLock = new();
#else
    private readonly object _propertiesLock = new();
#endif
    private readonly IMessageLogger? _messageLogger;

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
    internal TestContextImplementation(ITestMethod? testMethod, string? testClassFullName, IDictionary<string, object?> properties, IMessageLogger? messageLogger, TestRunCancellationToken? testRunCancellationToken)
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
        _cancellationTokenRegistration = testRunCancellationToken?.Register(CancelDelegate, this);
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
        GetTestContextMessagesStringBuilder().Append(msg);
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
        GetTestContextMessagesStringBuilder().Append(message);
    }

    /// <summary>
    /// When overridden in a derived class, used to write trace messages while the
    ///     test is running.
    /// </summary>
    /// <param name="message">The formatted string that contains the trace message.</param>
    public override void WriteLine(string? message)
    {
        string? msg = message?.Replace("\0", "\\0");
        GetTestContextMessagesStringBuilder().AppendLine(msg);
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
        GetTestContextMessagesStringBuilder().AppendLine(message);
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
    {
        if (_properties == null)
        {
            propertyValue = null;
            return false;
        }

        return _properties.TryGetValue(propertyName, out propertyValue);
    }

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
        if (propertiesToMerge is null)
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
    /// <returns>A read-only snapshot of the current properties.</returns>
    internal IReadOnlyDictionary<string, object?> CaptureLifecycleProperties()
    {
        Dictionary<string, object?> snapshot;
        lock (_propertiesLock)
        {
#pragma warning disable IDE0028 // Collection initialization can be simplified - capacity hint is intentional.
            snapshot = new Dictionary<string, object?>(_properties.Count);
#pragma warning restore IDE0028
            foreach (KeyValuePair<string, object?> kvp in _properties)
            {
                if (kvp.Key == FullyQualifiedTestClassNameLabel || kvp.Key == TestNameLabel)
                {
                    continue;
                }

                snapshot[kvp.Key] = kvp.Value;
            }
        }

        return new ReadOnlyDictionary<string, object?>(snapshot);
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

        var results = _testResultFiles.ToList();

        // clear the result files to handle data driven tests
        _testResultFiles.Clear();

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
        => _messageLogger?.SendMessage(messageLevel.ToTestMessageLevel(), message);
    #endregion

    /// <inheritdoc/>
    public void Dispose()
    {
        _cancellationTokenRegistration?.Dispose();
        _cancellationTokenRegistration = null;
    }

    internal readonly struct ScopedTestContextSetter : IDisposable
    {
        internal ScopedTestContextSetter(TestContext? testContext)
            => TestContext.Current = testContext;

        public void Dispose()
            => TestContext.Current = null;
    }

    internal static ScopedTestContextSetter SetCurrentTestContext(TestContext? testContext)
        => new(testContext);

    internal void WriteConsoleOut(char value)
        => GetOutStringBuilder().Append(value);

    internal void WriteConsoleOut(string? value)
        => GetOutStringBuilder().Append(value);

    internal void WriteConsoleOut(char[] buffer, int index, int count)
        => GetOutStringBuilder().Append(buffer, index, count);

    internal void WriteConsoleErr(char value)
        => GetErrStringBuilder().Append(value);

    internal void WriteConsoleErr(string? value)
        => GetErrStringBuilder().Append(value);

    internal void WriteConsoleErr(char[] buffer, int index, int count)
        => GetErrStringBuilder().Append(buffer, index, count);

    internal void WriteTrace(char value)
        => GetTraceStringBuilder().Append(value);

    internal void WriteTrace(string? value)
        => GetTraceStringBuilder().Append(value);

    private SynchronizedStringBuilder GetOutStringBuilder()
    {
        _ = _stdOutStringBuilder ?? Interlocked.CompareExchange(ref _stdOutStringBuilder, new SynchronizedStringBuilder(), null)!;
        return _stdOutStringBuilder;
    }

    private SynchronizedStringBuilder GetErrStringBuilder()
    {
        _ = _stdErrStringBuilder ?? Interlocked.CompareExchange(ref _stdErrStringBuilder, new SynchronizedStringBuilder(), null)!;
        return _stdErrStringBuilder;
    }

    private SynchronizedStringBuilder GetTraceStringBuilder()
    {
        _ = _traceStringBuilder ?? Interlocked.CompareExchange(ref _traceStringBuilder, new SynchronizedStringBuilder(), null)!;
        return _traceStringBuilder;
    }

    private SynchronizedStringBuilder GetTestContextMessagesStringBuilder()
    {
        _ = _testContextMessageStringBuilder ?? Interlocked.CompareExchange(ref _testContextMessageStringBuilder, new SynchronizedStringBuilder(), null)!;
        return _testContextMessageStringBuilder;
    }

    internal string? GetAndClearOutput()
        => _stdOutStringBuilder?.GetAndClear();

    internal string? GetAndClearError()
        => _stdErrStringBuilder?.GetAndClear();

    internal string? GetAndClearTrace()
        => _traceStringBuilder?.GetAndClear();
}
