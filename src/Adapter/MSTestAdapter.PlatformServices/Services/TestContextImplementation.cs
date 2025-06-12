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
public class TestContextImplementation : TestContext, ITestContext, IDisposable
{
    /// <summary>
    /// List of result files associated with the test.
    /// </summary>
    private readonly List<string> _testResultFiles;

    /// <summary>
    /// Writer on which the messages given by the user should be written.
    /// </summary>
    private readonly StringWriter _stringWriter;
    private readonly ThreadSafeStringWriter? _threadSafeStringWriter;

    /// <summary>
    /// Properties.
    /// </summary>
    private readonly Dictionary<string, object?> _properties;
    private readonly IMessageLogger? _messageLogger;
    private readonly CancellationTokenRegistration? _cancellationTokenRegistration;

    /// <summary>
    /// Specifies whether the writer is disposed or not.
    /// </summary>
    private bool _stringWriterDisposed;

    private bool _isDisposed;

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
    /// <param name="stringWriter">The writer where diagnostic messages are written to.</param>
    /// <param name="properties">Properties/configuration passed in.</param>
    /// <param name="messageLogger">The message logger to use.</param>
    /// <param name="testRunCancellationToken">The global test run cancellation token.</param>
    internal TestContextImplementation(ITestMethod? testMethod, StringWriter stringWriter, IDictionary<string, object?> properties, IMessageLogger messageLogger, TestRunCancellationToken? testRunCancellationToken)
        : this(testMethod, stringWriter, properties)
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

#if NETFRAMEWORK
        DebugEx.Assert(stringWriter != null, "StringWriter is not null");
#endif

        _stringWriter = stringWriter;

        // Cannot get this type in constructor directly, because all signatures for all platforms need to be the same.
        _threadSafeStringWriter = stringWriter as ThreadSafeStringWriter;
        _properties = testMethod is null
            ? new Dictionary<string, object?>(properties)
            : new Dictionary<string, object?>(properties)
            {
                [FullyQualifiedTestClassNameLabel] = testMethod.FullClassName,
                [ManagedTypeLabel] = testMethod.ManagedTypeName,
                [ManagedMethodLabel] = testMethod.ManagedMethodName,
                [TestNameLabel] = testMethod.Name,
            };

        _testResultFiles = [];
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

        _testResultFiles.Add(Path.GetFullPath(fileName));
    }

    /// <summary>
    /// When overridden in a derived class, used to write trace messages while the
    ///     test is running.
    /// </summary>
    /// <param name="message">The formatted string that contains the trace message.</param>
    public override void Write(string? message)
    {
        if (_stringWriterDisposed)
        {
            return;
        }

        try
        {
            string? msg = message?.Replace("\0", "\\0");
            _stringWriter.Write(msg);
        }
        catch (ObjectDisposedException)
        {
            _stringWriterDisposed = true;
        }
    }

    /// <summary>
    /// When overridden in a derived class, used to write trace messages while the
    ///     test is running.
    /// </summary>
    /// <param name="format">The string that contains the trace message.</param>
    /// <param name="args">Arguments to add to the trace message.</param>
    public override void Write(string format, params object?[] args)
    {
        if (_stringWriterDisposed)
        {
            return;
        }

        try
        {
            string message = string.Format(CultureInfo.CurrentCulture, format.Replace("\0", "\\0"), args);
            _stringWriter.Write(message);
        }
        catch (ObjectDisposedException)
        {
            _stringWriterDisposed = true;
        }
    }

    /// <summary>
    /// When overridden in a derived class, used to write trace messages while the
    ///     test is running.
    /// </summary>
    /// <param name="message">The formatted string that contains the trace message.</param>
    public override void WriteLine(string? message)
    {
        if (_stringWriterDisposed)
        {
            return;
        }

        try
        {
            string? msg = message?.Replace("\0", "\\0");
            _stringWriter.WriteLine(msg);
        }
        catch (ObjectDisposedException)
        {
            _stringWriterDisposed = true;
        }
    }

    /// <summary>
    /// When overridden in a derived class, used to write trace messages while the
    ///     test is running.
    /// </summary>
    /// <param name="format">The string that contains the trace message.</param>
    /// <param name="args">Arguments to add to the trace message.</param>
    public override void WriteLine(string format, params object?[] args)
    {
        if (_stringWriterDisposed)
        {
            return;
        }

        try
        {
            string message = string.Format(CultureInfo.CurrentCulture, format.Replace("\0", "\\0"), args);
            _stringWriter.WriteLine(message);
        }
        catch (ObjectDisposedException)
        {
            _stringWriterDisposed = true;
        }
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
        if (_testResultFiles.Count == 0)
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
        => _stringWriter.ToString();

    /// <summary>
    /// Clears the previous testContext writeline messages.
    /// </summary>
    public void ClearDiagnosticMessages()
        => _threadSafeStringWriter?.ToStringAndClear();

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
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// The dispose pattern.
    /// </summary>
    /// <param name="disposing">Whether to dispose managed state.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_isDisposed)
        {
            _isDisposed = true;

            if (disposing)
            {
                _cancellationTokenRegistration?.Dispose();
            }
        }
    }
}
