// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net.Sockets;
    using System.Threading;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ObjectModel;

    using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Internal implementation of TestContext exposed to the user.
    /// </summary>
    /// <remarks>
    /// The virtual string properties of the TestContext are retrieved from the property dictionary
    /// like GetProperty&lt;string&gt;("TestName") or GetProperty&lt;string&gt;("FullyQualifiedTestClassName");
    /// </remarks>
    public class TestContextImplementation : UTF.TestContext, ITestContext
    {
        private static readonly string FullyQualifiedTestClassNameLabel = nameof(FullyQualifiedTestClassName);
        private static readonly string ManagedTypeLabel = nameof(ManagedType);
        private static readonly string ManagedMethodLabel = nameof(ManagedMethod);
        private static readonly string TestNameLabel = nameof(TestName);

        /// <summary>
        /// List of result files associated with the test
        /// </summary>
        private readonly IList<string> testResultFiles;

        /// <summary>
        /// Properties
        /// </summary>
        private IDictionary<string, object> properties;

        /// <summary>
        /// Unit test outcome
        /// </summary>
        private UTF.UnitTestOutcome outcome;

        /// <summary>
        /// Test Method
        /// </summary>
        private ITestMethod testMethod;

        private ThreadSafeStringWriter threadSafeStringWriter;
        private bool stringWriterDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestContextImplementation"/> class.
        /// </summary>
        /// <param name="testMethod"> The test method. </param>
        /// <param name="writer"> A writer for logging. </param>
        /// <param name="properties"> The properties. </param>
        public TestContextImplementation(ITestMethod testMethod, StringWriter writer, IDictionary<string, object> properties)
        {
            Debug.Assert(testMethod != null, "TestMethod is not null");
            Debug.Assert(properties != null, "properties is not null");

            this.testMethod = testMethod;
            this.properties = new Dictionary<string, object>(properties);

            // Cannot get this type in constructor directly, because all signatures for all platforms need to be the same.
            this.threadSafeStringWriter = (ThreadSafeStringWriter)writer;
            this.InitializeProperties();
            this.testResultFiles = new List<string>();
            this.CancellationTokenSource = new CancellationTokenSource();
        }

        #region TestContext impl

        // Summary:
        //     You can use this property in a TestCleanup method to determine the outcome
        //     of a test that has run.
        //
        // Returns:
        //     A Microsoft.VisualStudio.TestTools.UnitTesting.UnitTestOutcome that states
        //     the outcome of a test that has run.
        public override UTF.UnitTestOutcome CurrentTestOutcome
        {
            get
            {
                return this.outcome;
            }
        }

        /// <inheritdoc/>
        public override string TestRunDirectory
        {
            get
            {
                return this.GetStringPropertyValue(TestContextPropertyStrings.TestRunDirectory);
            }
        }

        /// <inheritdoc/>
        public override string DeploymentDirectory
        {
            get
            {
                return this.GetStringPropertyValue(TestContextPropertyStrings.DeploymentDirectory);
            }
        }

        /// <inheritdoc/>
        public override string ResultsDirectory
        {
            get
            {
                return this.GetStringPropertyValue(TestContextPropertyStrings.ResultsDirectory);
            }
        }

        /// <inheritdoc/>
        public override string TestRunResultsDirectory
        {
            get
            {
                return this.GetStringPropertyValue(TestContextPropertyStrings.TestRunResultsDirectory);
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
                return this.GetStringPropertyValue(TestContextPropertyStrings.TestResultsDirectory);
            }
        }

        /// <inheritdoc/>
        public override string TestDir
        {
            get
            {
                return this.GetStringPropertyValue(TestContextPropertyStrings.TestDir);
            }
        }

        /// <inheritdoc/>
        public override string TestDeploymentDir
        {
            get
            {
                return this.GetStringPropertyValue(TestContextPropertyStrings.TestDeploymentDir);
            }
        }

        /// <inheritdoc/>
        public override string TestLogsDir
        {
            get
            {
                return this.GetStringPropertyValue(TestContextPropertyStrings.TestLogsDir);
            }
        }

        /// <summary>
        /// Gets fully-qualified name of the class containing the test method currently being executed
        /// </summary>
        /// <remarks>
        /// This property can be useful in attributes derived from ExpectedExceptionBaseAttribute.
        /// Those attributes have access to the test context, and provide messages that are included
        /// in the test results. Users can benefit from messages that include the fully-qualified
        /// class name in addition to the name of the test method currently being executed.
        /// </remarks>
        public override string FullyQualifiedTestClassName
        {
            get
            {
                return this.GetPropertyValue(FullyQualifiedTestClassNameLabel) as string;
            }
        }

        /// <summary>
        /// Gets name of the test method currently being executed
        /// </summary>
        public override string TestName
        {
            get
            {
                return this.GetPropertyValue(TestNameLabel) as string;
            }
        }

        /// <summary>
        /// Gets the test properties when overridden in a derived class.
        /// </summary>
        /// <returns>
        /// An System.Collections.IDictionary object that contains key/value pairs that
        ///  represent the test properties.
        /// </returns>
        public override IDictionary Properties
        {
            get
            {
                return this.properties as IDictionary;
            }
        }

        public UTF.TestContext Context
        {
            get
            {
                return this as UTF.TestContext;
            }
        }

        /// <summary>
        /// Adds a file name to the list in TestResult.ResultFileNames
        /// </summary>
        /// <param name="fileName">
        /// The file Name.
        /// </param>
        public override void AddResultFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                throw new ArgumentException(Resource.Common_CannotBeNullOrEmpty, nameof(fileName));
            }

            this.testResultFiles.Add(Path.GetFullPath(fileName));
        }

        /// <summary>
        /// Set the unit-test outcome
        /// </summary>
        /// <param name="outcome">The test outcome.</param>
        public void SetOutcome(UTF.UnitTestOutcome outcome)
        {
            this.outcome = outcome;
        }

        /// <summary>
        /// Returns whether property with parameter name is present or not
        /// </summary>
        /// <param name="propertyName">The property name.</param>
        /// <param name="propertyValue">Property value.</param>
        /// <returns>True if property with parameter name is present.</returns>
        public bool TryGetPropertyValue(string propertyName, out object propertyValue)
        {
            if (this.properties == null)
            {
                propertyValue = null;
                return false;
            }

            return this.properties.TryGetValue(propertyName, out propertyValue);
        }

        /// <summary>
        /// Adds the parameter name/value pair to property bag
        /// </summary>
        /// <param name="propertyName">The property name.</param>
        /// <param name="propertyValue">Property value.</param>
        public void AddProperty(string propertyName, string propertyValue)
        {
            if (this.properties == null)
            {
                this.properties = new Dictionary<string, object>();
            }

            this.properties.Add(propertyName, propertyValue);
        }

        /// <summary>
        /// Result files attached
        /// </summary>
        /// <returns>List of result files generated in run.</returns>
        public IList<string> GetResultFiles()
        {
            if (this.testResultFiles.Count == 0)
            {
                return null;
            }

            IList<string> results = this.testResultFiles.ToList();

            this.testResultFiles.Clear();

            return results;
        }

        /// <summary>
        /// When overridden in a derived class, used to write trace messages while the
        ///     test is running.
        /// </summary>
        /// <param name="message">The formatted string that contains the trace message.</param>
        public override void Write(string message)
        {
            if (this.stringWriterDisposed)
            {
                return;
            }

            try
            {
                var msg = message?.Replace("\0", "\\0");
                this.threadSafeStringWriter.Write(msg);
            }
            catch (ObjectDisposedException)
            {
                this.stringWriterDisposed = true;
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
            if (this.stringWriterDisposed)
            {
                return;
            }

            try
            {
                string message = string.Format(CultureInfo.CurrentCulture, format?.Replace("\0", "\\0"), args);
                this.threadSafeStringWriter.Write(message);
            }
            catch (ObjectDisposedException)
            {
                this.stringWriterDisposed = true;
            }
        }

        /// <summary>
        /// When overridden in a derived class, used to write trace messages while the
        ///     test is running.
        /// </summary>
        /// <param name="message">The formatted string that contains the trace message.</param>
        public override void WriteLine(string message)
        {
            if (this.stringWriterDisposed)
            {
                return;
            }

            try
            {
                var msg = message?.Replace("\0", "\\0");
                this.threadSafeStringWriter.WriteLine(msg);
            }
            catch (ObjectDisposedException)
            {
                this.stringWriterDisposed = true;
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
            if (this.stringWriterDisposed)
            {
                return;
            }

            try
            {
                string message = string.Format(CultureInfo.CurrentCulture, format?.Replace("\0", "\\0"), args);
                this.threadSafeStringWriter.WriteLine(message);
            }
            catch (ObjectDisposedException)
            {
                this.stringWriterDisposed = true;
            }
        }

        /// <summary>
        /// Gets messages from the testContext writeLines
        /// </summary>
        /// <returns>The test context messages added so far.</returns>
        public string GetDiagnosticMessages()
        {
            return this.threadSafeStringWriter.ToString();
        }

        /// <summary>
        /// Clears the previous testContext writeline messages.
        /// </summary>
        public void ClearDiagnosticMessages()
        {
            this.threadSafeStringWriter.ToStringAndClear();
        }

        public void SetDataRow(object dataRow)
        {
            // Do nothing.
        }

        public void SetDataConnection(object dbConnection)
        {
            // Do nothing.
        }

        #endregion

        /// <summary>
        /// Helper to safely fetch a property value.
        /// </summary>
        /// <param name="propertyName">Property Name</param>
        /// <returns>Property value</returns>
        private object GetPropertyValue(string propertyName)
        {
            this.properties.TryGetValue(propertyName, out var propertyValue);

            return propertyValue;
        }

        /// <summary>
        /// Helper to safely fetch a property value.
        /// </summary>
        /// <param name="propertyName">Property Name</param>
        /// <returns>Property value</returns>
        private string GetStringPropertyValue(string propertyName)
        {
            this.properties.TryGetValue(propertyName, out var propertyValue);
            return propertyValue as string;
        }

        /// <summary>
        /// Helper to initialize the properties.
        /// </summary>
        private void InitializeProperties()
        {
            this.properties[FullyQualifiedTestClassNameLabel] = this.testMethod.FullClassName;
            this.properties[ManagedTypeLabel] = this.testMethod.ManagedTypeName;
            this.properties[ManagedMethodLabel] = this.testMethod.ManagedMethodName;
            this.properties[TestNameLabel] = this.testMethod.Name;
        }
    }

#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName
}
