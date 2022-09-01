// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ObjectModel;

using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Internal implementation of TestContext exposed to the user.
/// The virtual string properties of the TestContext are retrieved from the property dictionary
/// like GetProperty&lt;string&gt;("TestName") or GetProperty&lt;string&gt;("FullyQualifiedTestClassName");
/// </summary>
public class TestContextImplementation : UTF.TestContext, ITestContext
{
    /// <summary>
    /// List of result files associated with the test
    /// </summary>
    private readonly IList<string> _testResultFiles;

    /// <summary>
    /// Properties
    /// </summary>
    private IDictionary<string, object> _properties;

    /// <summary>
    /// Unit test outcome
    /// </summary>
    private UTF.UnitTestOutcome _outcome;

    /// <summary>
    /// Writer on which the messages given by the user should be written
    /// </summary>
    private readonly ThreadSafeStringWriter _threadSafeStringWriter;

    /// <summary>
    /// Specifies whether the writer is disposed or not
    /// </summary>
    private bool _stringWriterDisposed = false;

    /// <summary>
    /// Test Method
    /// </summary>
    private readonly ITestMethod _testMethod;

    /// <summary>
    /// DB connection for test context
    /// </summary>
    private DbConnection _dbConnection;

    /// <summary>
    /// Data row for TestContext
    /// </summary>
    private DataRow _dataRow;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestContextImplementation"/> class.
    /// </summary>
    /// <param name="testMethod">The test method.</param>
    /// <param name="stringWriter">The writer where diagnostic messages are written to.</param>
    /// <param name="properties">Properties/configuration passed in.</param>
    public TestContextImplementation(ITestMethod testMethod, StringWriter stringWriter, IDictionary<string, object> properties)
    {
        Debug.Assert(testMethod != null, "TestMethod is not null");
        Debug.Assert(stringWriter != null, "StringWriter is not null");
        Debug.Assert(properties != null, "properties is not null");

        _testMethod = testMethod;

        // Cannot get this type in constructor directly, because all signatures for all platforms need to be the same.
        _threadSafeStringWriter = (ThreadSafeStringWriter)stringWriter;
        _properties = new Dictionary<string, object>(properties);
        CancellationTokenSource = new CancellationTokenSource();
        InitializeProperties();

        _testResultFiles = new List<string>();
    }

    #region TestContext impl

    /// <inheritdoc/>
    public override UTF.UnitTestOutcome CurrentTestOutcome
    {
        get
        {
            return _outcome;
        }
    }

    /// <inheritdoc/>
    public override DbConnection DataConnection
    {
        get
        {
            return _dbConnection;
        }
    }

    /// <inheritdoc/>
    public override DataRow DataRow
    {
        get
        {
            return _dataRow;
        }
    }

    /// <inheritdoc/>
    public override IDictionary Properties
    {
        get
        {
            return _properties as IDictionary;
        }
    }

    /// <inheritdoc/>
    public override string TestRunDirectory
    {
        get
        {
            return GetStringPropertyValue(TestContextPropertyStrings.TestRunDirectory);
        }
    }

    /// <inheritdoc/>
    public override string DeploymentDirectory
    {
        get
        {
            return GetStringPropertyValue(TestContextPropertyStrings.DeploymentDirectory);
        }
    }

    /// <inheritdoc/>
    public override string ResultsDirectory
    {
        get
        {
            return GetStringPropertyValue(TestContextPropertyStrings.ResultsDirectory);
        }
    }

    /// <inheritdoc/>
    public override string TestRunResultsDirectory
    {
        get
        {
            return GetStringPropertyValue(TestContextPropertyStrings.TestRunResultsDirectory);
        }
    }

    /// <inheritdoc/>
    [SuppressMessage("Microsoft.Naming", "CA1702:CompoundWordsShouldBeCasedCorrectly", Justification = "TestResultsDirectory is what we need.")]
    public override string TestResultsDirectory
    {
        get
        {
            // In MSTest, it is actually "In\697105f7-004f-42e8-bccf-eb024870d3e9\User1", but
            // we are setting it to "In" only because MSTest does not create this directory.
            return GetStringPropertyValue(TestContextPropertyStrings.TestResultsDirectory);
        }
    }

    /// <inheritdoc/>
    public override string TestDir
    {
        get
        {
            return GetStringPropertyValue(TestContextPropertyStrings.TestDir);
        }
    }

    /// <inheritdoc/>
    public override string TestDeploymentDir
    {
        get
        {
            return GetStringPropertyValue(TestContextPropertyStrings.TestDeploymentDir);
        }
    }

    /// <inheritdoc/>
    public override string TestLogsDir
    {
        get
        {
            return GetStringPropertyValue(TestContextPropertyStrings.TestLogsDir);
        }
    }

    /// <inheritdoc/>
    public override string FullyQualifiedTestClassName
    {
        get
        {
            return GetStringPropertyValue(TestContextPropertyStrings.FullyQualifiedTestClassName);
        }
    }

    /// <inheritdoc/>
    public override string ManagedType
    {
        get
        {
            return GetStringPropertyValue(TestContextPropertyStrings.ManagedType);
        }
    }

    /// <inheritdoc/>
    public override string ManagedMethod
    {
        get
        {
            return GetStringPropertyValue(TestContextPropertyStrings.ManagedMethod);
        }
    }

    /// <inheritdoc/>
    public override string TestName
    {
        get
        {
            return GetStringPropertyValue(TestContextPropertyStrings.TestName);
        }
    }

    public UTF.TestContext Context
    {
        get
        {
            return this as UTF.TestContext;
        }
    }

    /// <inheritdoc/>
    public override void AddResultFile(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            throw new ArgumentException(Resource.Common_CannotBeNullOrEmpty, nameof(fileName));
        }

        _testResultFiles.Add(Path.GetFullPath(fileName));
    }

    /// <inheritdoc/>
    public override void BeginTimer(string timerName)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override void EndTimer(string timerName)
    {
        throw new NotSupportedException();
    }

    /// <summary>
    /// When overridden in a derived class, used to write trace messages while the
    ///     test is running.
    /// </summary>
    /// <param name="message">The formatted string that contains the trace message.</param>
    public override void Write(string message)
    {
        if (_stringWriterDisposed)
        {
            return;
        }

        try
        {
            var msg = message?.Replace("\0", "\\0");
            _threadSafeStringWriter.Write(msg);
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
    public override void Write(string format, params object[] args)
    {
        if (_stringWriterDisposed)
        {
            return;
        }

        try
        {
            string message = string.Format(CultureInfo.CurrentCulture, format?.Replace("\0", "\\0"), args);
            _threadSafeStringWriter.Write(message);
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
    public override void WriteLine(string message)
    {
        if (_stringWriterDisposed)
        {
            return;
        }

        try
        {
            var msg = message?.Replace("\0", "\\0");
            _threadSafeStringWriter.WriteLine(msg);
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
    public override void WriteLine(string format, params object[] args)
    {
        if (_stringWriterDisposed)
        {
            return;
        }

        try
        {
            string message = string.Format(CultureInfo.CurrentCulture, format?.Replace("\0", "\\0"), args);
            _threadSafeStringWriter.WriteLine(message);
        }
        catch (ObjectDisposedException)
        {
            _stringWriterDisposed = true;
        }
    }

    /// <summary>
    /// Set the unit-test outcome
    /// </summary>
    /// <param name="outcome">The test outcome.</param>
    public void SetOutcome(UTF.UnitTestOutcome outcome)
    {
        _outcome = ToUTF(outcome);
    }

    /// <summary>
    /// Set data row for particular run of TestMethod.
    /// </summary>
    /// <param name="dataRow">data row.</param>
    public void SetDataRow(object dataRow)
    {
        _dataRow = dataRow as DataRow;
    }

    /// <summary>
    /// Set connection for TestContext
    /// </summary>
    /// <param name="dbConnection">db Connection.</param>
    public void SetDataConnection(object dbConnection)
    {
        _dbConnection = dbConnection as DbConnection;
    }

    /// <summary>
    /// Returns whether property with parameter name is present or not
    /// </summary>
    /// <param name="propertyName">The property name.</param>
    /// <param name="propertyValue">The property value.</param>
    /// <returns>True if found.</returns>
    public bool TryGetPropertyValue(string propertyName, out object propertyValue)
    {
        if (_properties == null)
        {
            propertyValue = null;
            return false;
        }

        return _properties.TryGetValue(propertyName, out propertyValue);
    }

    /// <summary>
    /// Adds the parameter name/value pair to property bag
    /// </summary>
    /// <param name="propertyName">The property name.</param>
    /// <param name="propertyValue">The property value.</param>
    public void AddProperty(string propertyName, string propertyValue)
    {
        _properties ??= new Dictionary<string, object>();

        _properties.Add(propertyName, propertyValue);
    }

    /// <summary>
    /// Result files attached
    /// </summary>
    /// <returns>Results files generated in run.</returns>
    public IList<string> GetResultFiles()
    {
        if (!_testResultFiles.Any())
        {
            return null;
        }

        List<string> results = _testResultFiles.ToList();

        // clear the result files to handle data driven tests
        _testResultFiles.Clear();

        return results;
    }

    /// <summary>
    /// Gets messages from the testContext writeLines
    /// </summary>
    /// <returns>The test context messages added so far.</returns>
    public string GetDiagnosticMessages()
    {
        return _threadSafeStringWriter.ToString();
    }

    /// <summary>
    /// Clears the previous testContext writeline messages.
    /// </summary>
    public void ClearDiagnosticMessages()
    {
        _threadSafeStringWriter.ToStringAndClear();
    }

    #endregion

    /// <summary>
    /// Converts the parameter outcome to UTF outcome
    /// </summary>
    /// <param name="outcome">The UTF outcome.</param>
    /// <returns>test outcome</returns>
    private static UTF.UnitTestOutcome ToUTF(UTF.UnitTestOutcome outcome)
    {
        switch (outcome)
        {
            case UTF.UnitTestOutcome.Error:
            case UTF.UnitTestOutcome.Failed:
            case UTF.UnitTestOutcome.Inconclusive:
            case UTF.UnitTestOutcome.Passed:
            case UTF.UnitTestOutcome.Timeout:
            case UTF.UnitTestOutcome.InProgress:
                return outcome;

            default:
                Debug.Fail("Unknown outcome " + outcome);
                return UTF.UnitTestOutcome.Unknown;
        }
    }

    /// <summary>
    /// Helper to safely fetch a property value.
    /// </summary>
    /// <param name="propertyName">Property Name</param>
    /// <returns>Property value</returns>
    private string GetStringPropertyValue(string propertyName)
    {
        _properties.TryGetValue(propertyName, out var propertyValue);
        return propertyValue as string;
    }

    /// <summary>
    /// Helper to initialize the properties.
    /// </summary>
    private void InitializeProperties()
    {
        _properties[TestContextPropertyStrings.FullyQualifiedTestClassName] = _testMethod.FullClassName;
        _properties[TestContextPropertyStrings.ManagedType] = _testMethod.ManagedTypeName;
        _properties[TestContextPropertyStrings.ManagedMethod] = _testMethod.ManagedMethodName;
        _properties[TestContextPropertyStrings.TestName] = _testMethod.Name;
    }
}

#endif
