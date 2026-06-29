// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
using System.Data;
using System.Data.Common;
#endif

using System.ComponentModel;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Used to store information that is provided to unit tests.
/// </summary>
public abstract class TestContext
{
    private static readonly AsyncLocal<TestContext?> CurrentContext = new();

    /// <summary>
    /// Gets the current <see cref="TestContext"/> instance.
    /// </summary>
    /// <remarks>
    /// This property returns the context for the currently executing test. When accessed outside of a test execution,
    /// it returns <see langword="null"/>.
    /// </remarks>
    [Experimental("MSTESTEXP", UrlFormat = "https://aka.ms/mstest/diagnostics#{0}")]
    public static TestContext? Current
    {
        get => CurrentContext.Value;
        internal set => CurrentContext.Value = value;
    }

    internal static readonly string FullyQualifiedTestClassNameLabel = nameof(FullyQualifiedTestClassName);
    internal static readonly string TestNameLabel = nameof(TestName);
#if !WINDOWS_UWP && !WIN_UI
    internal static readonly string TestRunDirectoryLabel = nameof(TestRunDirectory);
    internal static readonly string DeploymentDirectoryLabel = nameof(DeploymentDirectory);
    internal static readonly string ResultsDirectoryLabel = nameof(ResultsDirectory);
    internal static readonly string TestRunResultsDirectoryLabel = nameof(TestRunResultsDirectory);
    internal static readonly string TestResultsDirectoryLabel = nameof(TestResultsDirectory);
#endif

    /// <summary>
    /// Gets test properties for a test.
    /// </summary>
    public abstract IDictionary<string, object?> Properties { get; }

    /// <summary>
    /// Gets or sets the cancellation token source. This token source is canceled when test times out. Also when explicitly canceled the test will be aborted.
    /// </summary>
    // Disposing isn't important per https://github.com/dotnet/runtime/issues/29970#issuecomment-717840778
    [EditorBrowsable(EditorBrowsableState.Never)]
    public virtual CancellationTokenSource CancellationTokenSource { get; protected internal set; } = new();

    /// <summary>
    /// Gets the cancellation token. This token is canceled when test times out. Also when explicitly canceled the test will be aborted.
    /// </summary>
    public CancellationToken CancellationToken => CancellationTokenSource.Token;

    /// <summary>
    /// Gets or sets the test data for the test method being executed.
    /// </summary>
    public object?[]? TestData { get; protected set; }

    /// <summary>
    /// Gets or sets the test display name for the test method being executed.
    /// </summary>
    public string? TestDisplayName { get; protected set; }

#if NETFRAMEWORK
    /// <summary>
    /// Gets the current data row when test is used for data driven testing.
    /// </summary>
    public abstract DataRow? DataRow { get; }

    /// <summary>
    /// Gets current data connection row when test is used for data driven testing.
    /// </summary>
    public abstract DbConnection? DataConnection { get; }
#endif

#if !WINDOWS_UWP && !WIN_UI
    #region Test run deployment directories

    /// <summary>
    /// Gets the top-level directory for the test run, under which deployed files and result files are stored.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the root of the layout used by the other deployment directory properties. A typical run produces:
    /// </para>
    /// <code>
    /// &lt;solution&gt;\TestResults\&lt;run-guid&gt;        // TestRunDirectory
    ///     \In                                  // ResultsDirectory and TestResultsDirectory
    ///         \&lt;MachineName&gt;                    // TestRunResultsDirectory
    ///     \Out                                 // DeploymentDirectory
    /// </code>
    /// </remarks>
    public virtual string? TestRunDirectory => GetProperty<string>(TestRunDirectoryLabel);

    /// <summary>
    /// Gets the directory for files deployed for the test run (the "Out" directory). This is typically the <c>Out</c> subdirectory of <see cref="TestRunDirectory"/>.
    /// </summary>
    /// <remarks>
    /// When app domains are disabled, this can instead point at the test assembly directory.
    /// </remarks>
    public virtual string? DeploymentDirectory => GetProperty<string>(DeploymentDirectoryLabel);

    /// <summary>
    /// Gets the base directory for results from the test run (the "In" directory). This is typically the <c>In</c> subdirectory of <see cref="TestRunDirectory"/>.
    /// </summary>
    /// <remarks>
    /// In the current implementation this returns the same path as <see cref="TestResultsDirectory"/> (the <c>In</c> directory).
    /// When run directory information is unavailable, the platform services layer can fall back to the application base directory.
    /// </remarks>
    public virtual string? ResultsDirectory => GetProperty<string>(ResultsDirectoryLabel);

    /// <summary>
    /// Gets the per-machine directory for test run result files. This is the <c>&lt;MachineName&gt;</c> subdirectory of <see cref="ResultsDirectory"/>
    /// (i.e. <c>In\&lt;MachineName&gt;</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Despite the similar names, this directory is typically a <b>child</b> of <see cref="TestResultsDirectory"/>, not the other way around.
    /// For example, with <see cref="ResultsDirectory"/> set to <c>...\In</c> on a machine named <c>BUILD01</c>:
    /// </para>
    /// <code>
    /// TestResultsDirectory    => ...\In
    /// TestRunResultsDirectory => ...\In\BUILD01
    /// </code>
    /// <para>
    /// When run directory information is unavailable, the platform services layer can return the same path for both properties.
    /// </para>
    /// </remarks>
    public virtual string? TestRunResultsDirectory => GetProperty<string>(TestRunResultsDirectoryLabel);

    /// <summary>
    /// Gets the directory for test result files (the "In" directory). This is the same path as <see cref="ResultsDirectory"/>
    /// and is typically the parent of <see cref="TestRunResultsDirectory"/>. When run directory information is unavailable,
    /// the platform services layer can return the same path for both properties.
    /// </summary>
    public virtual string? TestResultsDirectory => GetProperty<string>(TestResultsDirectoryLabel);

    #endregion
#endif

    /// <summary>
    /// Gets the Fully-qualified name of the class containing the test method currently being executed.
    /// </summary>
    public virtual string FullyQualifiedTestClassName => GetProperty<string>(FullyQualifiedTestClassNameLabel)
        ?? throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, FrameworkMessages.InvalidAccessToTestContextProperty, nameof(FullyQualifiedTestClassName)));

    /// <summary>
    /// Gets the name of the test method currently being executed.
    /// </summary>
    public virtual string TestName => GetProperty<string>(TestNameLabel)
        ?? throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, FrameworkMessages.InvalidAccessToTestContextProperty, nameof(TestName)));

    /// <summary>
    /// Gets the current test outcome.
    /// </summary>
    public virtual UnitTestOutcome CurrentTestOutcome => UnitTestOutcome.Unknown;

    /// <summary>
    /// Gets or sets the exception that occurred in the TestInitialize or TestMethod steps.
    /// </summary>
    /// <remarks>
    /// The property is always <c>null</c> when accessed during the TestInitialize or TestMethod steps.
    /// </remarks>
    public Exception? TestException { get; protected set; }

    /// <summary>
    /// Gets the current attempt of the test run. This property is relevant when
    /// using <see cref="RetryAttribute"/> (or any implementation of <see cref="RetryBaseAttribute"/>).
    /// On the first run, this property is set to 1.
    /// On subsequent retries, the value is incremented.
    /// </summary>
    public int TestRunCount { get; internal set; }

    /// <summary>
    /// Adds a file name to the list in TestResult.ResultFileNames.
    /// </summary>
    /// <param name="fileName">
    /// The file Name.
    /// </param>
    public abstract void AddResultFile(string fileName);

    /// <summary>
    /// Used to write trace messages while the test is running.
    /// </summary>
    /// <param name="message">formatted message string.</param>
    public abstract void Write(string? message);

    /// <summary>
    /// Used to write trace messages while the test is running.
    /// </summary>
    /// <param name="format">format string.</param>
    /// <param name="args">the arguments.</param>
    public abstract void Write(string format, params object?[] args);

    /// <summary>
    /// Used to write trace messages while the test is running.
    /// </summary>
    /// <param name="message">formatted message string.</param>
    public abstract void WriteLine(string? message);

    /// <summary>
    /// Used to write trace messages while the test is running.
    /// </summary>
    /// <param name="format">format string.</param>
    /// <param name="args">the arguments.</param>
    public abstract void WriteLine(string format, params object?[] args);

    /// <summary>
    /// Used to write trace messages while the test is running.
    /// </summary>
    /// <param name="messageLevel">The message level.</param>
    /// <param name="message">The message.</param>
    public abstract void DisplayMessage(MessageLevel messageLevel, string message);

    private T? GetProperty<T>(string name)
        where T : class
    {
        DebugEx.Assert(Properties is not null, "Properties is null");
#if WINDOWS_UWP || WIN_UI
        if (!Properties.TryGetValue(name, out object? propertyValue))
        {
            return null;
        }
#else
        // This old API doesn't throw when key is not found, but returns null.
        object? propertyValue = Properties[name];
#endif

        // If propertyValue has a value, but it's not the right type
        if (propertyValue is not null and not T)
        {
            Debug.Fail("How did an invalid value get in here?");
            throw new InvalidCastException(string.Format(CultureInfo.CurrentCulture, FrameworkMessages.InvalidPropertyType, name, propertyValue.GetType(), typeof(T)));
        }

        return (T?)propertyValue;
    }
}
