// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
using System.Data;
using System.Data.Common;
#endif

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
#if NET6_0_OR_GREATER
[Obsolete(FrameworkConstants.PublicTypeObsoleteMessage, DiagnosticId = "MSTESTOBS")]
#else
[Obsolete(FrameworkConstants.PublicTypeObsoleteMessage)]
#endif
public sealed class TestContextImplementation : TestContext, ITestContext, IDisposable
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
        public override string ToString()
            => _builder.ToString();
    }

    private static readonly AsyncLocal<TestContextImplementation?> CurrentTestContextAsyncLocal = new();

    // This should be removed. Don't rely on it.
    // We only keep it for public constructor, but we don't pass it by the adapter.
    private readonly StringWriter? _stringWriter;

    /// <summary>
    /// Properties.
    /// </summary>
    private readonly Dictionary<string, object?> _properties;
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
    /// <param name="properties">Properties/configuration passed in.</param>
    /// <param name="messageLogger">The message logger to use.</param>
    /// <param name="testRunCancellationToken">The global test run cancellation token.</param>
    internal TestContextImplementation(ITestMethod? testMethod, IDictionary<string, object?> properties, IMessageLogger messageLogger, TestRunCancellationToken? testRunCancellationToken)
        : this(testMethod, null!, properties)
    {
        _messageLogger = messageLogger;
        _cancellationTokenRegistration = testRunCancellationToken?.Register(CancelDelegate, this);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TestContextImplementation"/> class.
    /// </summary>
    /// <param name="testMethod">The test method.</param>
    /// <param name="stringWriter">The writer where diagnostic messages are written to.</param>
    /// <param name="properties">Properties/configuration passed in.</param>
    public TestContextImplementation(ITestMethod? testMethod, StringWriter stringWriter, IDictionary<string, object?> properties)
    {
        // testMethod can be null when running ForceCleanup (done when reaching --maximum-failed-tests.
        DebugEx.Assert(properties != null, "properties is not null");

        _stringWriter = stringWriter;
        if (testMethod is null)
        {
            _properties = new Dictionary<string, object?>(properties);
        }
        else
        {
            _properties = new Dictionary<string, object?>(properties.Count + 4);
            foreach (KeyValuePair<string, object?> kvp in properties)
            {
                _properties[kvp.Key] = kvp.Value;
            }

            _properties[FullyQualifiedTestClassNameLabel] = testMethod.FullClassName;
            _properties[ManagedTypeLabel] = testMethod.ManagedTypeName;
            _properties[ManagedMethodLabel] = testMethod.ManagedMethodName;
            _properties[TestNameLabel] = testMethod.Name;
        }
    }

    internal static TestContextImplementation? CurrentTestContext => CurrentTestContextAsyncLocal.Value;

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
    public override IDictionary Properties => _properties;

#if !WINDOWS_UWP && !WIN_UI
    /// <inheritdoc/>
    public override string? TestRunDirectory => base.TestRunDirectory;

    /// <inheritdoc/>
    public override string? DeploymentDirectory => base.DeploymentDirectory;

    /// <inheritdoc/>
    public override string? ResultsDirectory => base.ResultsDirectory;

    /// <inheritdoc/>
    public override string? TestRunResultsDirectory => base.TestRunResultsDirectory;

    /// <inheritdoc/>
    public override string? TestResultsDirectory => base.TestResultsDirectory;

    /// <inheritdoc/>
    public override string FullyQualifiedTestClassName => base.FullyQualifiedTestClassName!;

#if NETFRAMEWORK
    /// <inheritdoc/>
    public override string ManagedType => base.ManagedType!;

    /// <inheritdoc/>
    public override string ManagedMethod => base.ManagedMethod!;
#endif

    /// <inheritdoc/>
    public override string TestName => base.TestName!;
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
        GetTestContextMessagesStringBuilder().Append(msg);
        _stringWriter?.Write(msg);
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
        _stringWriter?.Write(message);
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
        _stringWriter?.Write(msg);
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
        _stringWriter?.Write(message);
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
    {
        _testContextMessageStringBuilder?.Clear();
        (_stringWriter as ThreadSafeStringWriter)?.ToStringAndClear();
    }

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
        internal ScopedTestContextSetter(TestContextImplementation? testContext)
            => CurrentTestContextAsyncLocal.Value = testContext;

        public void Dispose()
            => CurrentTestContextAsyncLocal.Value = null;
    }

    internal static ScopedTestContextSetter SetCurrentTestContext(TestContextImplementation? testContext)
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
        // Prefer writing to the current test context instead of 'this'.
        // This is just a hack to preserve backward compatibility.
        // It's relevant for cases where users store 'TestContext' in a static field.
        // Then, they write to the "wrong" test context.
        // Here, we are correcting user's fault by finding out the correct TestContext that should receive the message.
        TestContextImplementation @this = CurrentTestContext ?? this;
        _ = @this._testContextMessageStringBuilder ?? Interlocked.CompareExchange(ref @this._testContextMessageStringBuilder, new SynchronizedStringBuilder(), null)!;
        return @this._testContextMessageStringBuilder;
    }

    internal string? GetOut()
        => _stdOutStringBuilder?.ToString();

    internal string? GetErr()
        => _stdErrStringBuilder?.ToString();

    internal string? GetTrace()
        => _traceStringBuilder?.ToString();
}
