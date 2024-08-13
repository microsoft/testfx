// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
#if NETFRAMEWORK
using System.Data;
using System.Data.Common;
#endif
using System.Diagnostics;
using System.Globalization;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Used to store information that is provided to unit tests.
/// </summary>
public abstract class TestContext
{
    internal static readonly string FullyQualifiedTestClassNameLabel = nameof(FullyQualifiedTestClassName);
    internal static readonly string ManagedTypeLabel = nameof(ManagedType);
    internal static readonly string ManagedMethodLabel = nameof(ManagedMethod);
    internal static readonly string TestNameLabel = nameof(TestName);
#if WINDOWS_UWP || WIN_UI
    internal static readonly string TestRunDirectoryLabel = "TestRunDirectory";
    internal static readonly string DeploymentDirectoryLabel = "DeploymentDirectory";
    internal static readonly string ResultsDirectoryLabel = "ResultsDirectory";
    internal static readonly string TestRunResultsDirectoryLabel = "TestRunResultsDirectory";
    internal static readonly string TestResultsDirectoryLabel = "TestResultsDirectory";
    internal static readonly string TestDirLabel = "TestDir";
    internal static readonly string TestDeploymentDirLabel = "TestDeploymentDir";
    internal static readonly string TestLogsDirLabel = "TestLogsDir";
#else
    internal static readonly string TestRunDirectoryLabel = nameof(TestRunDirectory);
    internal static readonly string DeploymentDirectoryLabel = nameof(DeploymentDirectory);
    internal static readonly string ResultsDirectoryLabel = nameof(ResultsDirectory);
    internal static readonly string TestRunResultsDirectoryLabel = nameof(TestRunResultsDirectory);
    internal static readonly string TestResultsDirectoryLabel = nameof(TestResultsDirectory);
    [Obsolete("Remove when related property is removed.")]
    internal static readonly string TestDirLabel = nameof(TestDir);
    [Obsolete("Remove when related property is removed.")]
    internal static readonly string TestDeploymentDirLabel = nameof(TestDeploymentDir);
    [Obsolete("Remove when related property is removed.")]
    internal static readonly string TestLogsDirLabel = nameof(TestLogsDir);
#endif

    /// <summary>
    /// Gets test properties for a test.
    /// </summary>
    public abstract IDictionary Properties { get; }

    /// <summary>
    /// Gets or sets the cancellation token source. This token source is canceled when test times out. Also when explicitly canceled the test will be aborted.
    /// </summary>
    public virtual CancellationTokenSource CancellationTokenSource { get; protected set; } = new();

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
    /// Gets base directory for the test run, under which deployed files and result files are stored.
    /// </summary>
    public virtual string? TestRunDirectory => GetProperty<string>(TestRunDirectoryLabel);

    /// <summary>
    /// Gets directory for files deployed for the test run. Typically a subdirectory of <see cref="TestRunDirectory"/>.
    /// </summary>
    public virtual string? DeploymentDirectory => GetProperty<string>(DeploymentDirectoryLabel);

    /// <summary>
    /// Gets base directory for results from the test run. Typically a subdirectory of <see cref="TestRunDirectory"/>.
    /// </summary>
    public virtual string? ResultsDirectory => GetProperty<string>(ResultsDirectoryLabel);

    /// <summary>
    /// Gets directory for test run result files. Typically a subdirectory of <see cref="ResultsDirectory"/>.
    /// </summary>
    public virtual string? TestRunResultsDirectory => GetProperty<string>(TestRunResultsDirectoryLabel);

    /// <summary>
    /// Gets directory for test result files.
    /// </summary>
    // In MSTest, it is actually "In\697105f7-004f-42e8-bccf-eb024870d3e9\User1", but we are setting it to "In" only
    // because MSTest does not create the GUID directory.
    public virtual string? TestResultsDirectory => GetProperty<string>(TestResultsDirectoryLabel);

    #region Old names, for backwards compatibility

    /// <summary>
    /// Gets base directory for the test run, under which deployed files and result files are stored.
    /// Same as <see cref="TestRunDirectory"/>. Use that property instead.
    /// </summary>
    [Obsolete("This property is deprecated, use TestRunDirectory instead. It will be removed in next version.")]
    public virtual string? TestDir => GetProperty<string>(TestDirLabel);

    /// <summary>
    /// Gets directory for files deployed for the test run. Typically a subdirectory of <see cref="TestRunDirectory"/>.
    /// Same as <see cref="DeploymentDirectory"/>. Use that property instead.
    /// </summary>
    [Obsolete("This property is deprecated, use DeploymentDirectory instead. It will be removed in next version.")]
    public virtual string? TestDeploymentDir => GetProperty<string>(TestDeploymentDirLabel);

    /// <summary>
    /// Gets directory for test run result files. Typically a subdirectory of <see cref="ResultsDirectory"/>.
    /// Same as <see cref="TestRunResultsDirectory"/>. Use that property for test run result files, or
    /// <see cref="TestResultsDirectory"/> for test-specific result files instead.
    /// </summary>
    [Obsolete("This property is deprecated, use TestRunResultsDirectory for test run result files or TestResultsDirectory for test-specific result files instead. It will be removed in next version.")]
    public virtual string? TestLogsDir => GetProperty<string>(TestLogsDirLabel);

    #endregion

    #endregion
#endif

    /// <summary>
    /// Gets the Fully-qualified name of the class containing the test method currently being executed.
    /// </summary>
    /// <remarks>
    /// This property can be useful in attributes derived from ExpectedExceptionBaseAttribute.
    /// Those attributes have access to the test context, and provide messages that are included
    /// in the test results. Users can benefit from messages that include the fully-qualified
    /// class name in addition to the name of the test method currently being executed.
    /// </remarks>
    public virtual string? FullyQualifiedTestClassName => GetProperty<string>(FullyQualifiedTestClassNameLabel);

    /// <summary>
    /// Gets the fully specified type name metadata format.
    /// </summary>
    public virtual string? ManagedType => GetProperty<string>(ManagedTypeLabel);

    /// <summary>
    /// Gets the fully specified method name metadata format.
    /// </summary>
    public virtual string? ManagedMethod => GetProperty<string>(ManagedMethodLabel);

    /// <summary>
    /// Gets the name of the test method currently being executed.
    /// </summary>
    public virtual string? TestName => GetProperty<string>(TestNameLabel);

    /// <summary>
    /// Gets the current test outcome.
    /// </summary>
    public virtual UnitTestOutcome CurrentTestOutcome => UnitTestOutcome.Unknown;

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

    private T? GetProperty<T>(string name)
        where T : class
    {
        DebugEx.Assert(Properties is not null, "Properties is null");
#if WINDOWS_UWP || WIN_UI
        if (!((System.Collections.Generic.IDictionary<string, object>)Properties).TryGetValue(name, out object? propertyValue))
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
