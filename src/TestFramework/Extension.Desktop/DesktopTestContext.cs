// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
    using System;
    using System.Collections;
    using System.Data;
    using System.Data.Common;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Threading;

    /// <summary>
    /// Used to store information that is provided to unit tests.
    /// </summary>
    public abstract class TestContext
    {
        /// <summary>
        /// Gets test properties for a test.
        /// </summary>
        public abstract IDictionary Properties { get; }

        /// <summary>
        /// Gets or sets the cancellation token source. This token source is canceled when test times out. Also when explicitly canceled the test will be aborted
        /// </summary>
        public virtual CancellationTokenSource CancellationTokenSource { get; protected set; }

        /// <summary>
        /// Gets the current data row when test is used for data driven testing.
        /// </summary>
        public abstract DataRow DataRow { get; }

        /// <summary>
        /// Gets current data connection row when test is used for data driven testing.
        /// </summary>
        public abstract DbConnection DataConnection { get; }

        #region Test run deployment directories

        /// <summary>
        /// Gets base directory for the test run, under which deployed files and result files are stored.
        /// </summary>
        public virtual string TestRunDirectory => this.GetProperty<string>("TestRunDirectory");

        /// <summary>
        /// Gets directory for files deployed for the test run. Typically a subdirectory of <see cref="TestRunDirectory"/>.
        /// </summary>
        public virtual string DeploymentDirectory => this.GetProperty<string>("DeploymentDirectory");

        /// <summary>
        /// Gets base directory for results from the test run. Typically a subdirectory of <see cref="TestRunDirectory"/>.
        /// </summary>
        public virtual string ResultsDirectory => this.GetProperty<string>("ResultsDirectory");

        /// <summary>
        /// Gets directory for test run result files. Typically a subdirectory of <see cref="ResultsDirectory"/>.
        /// </summary>
        public virtual string TestRunResultsDirectory => this.GetProperty<string>("TestRunResultsDirectory");

        /// <summary>
        /// Gets directory for test result files.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1702:CompoundWordsShouldBeCasedCorrectly", Justification = "Compat")]
        public virtual string TestResultsDirectory => this.GetProperty<string>("TestResultsDirectory");

        #region Old names, for backwards compatibility

        /// <summary>
        /// Gets base directory for the test run, under which deployed files and result files are stored.
        /// Same as <see cref="TestRunDirectory"/>. Use that property instead.
        /// </summary>
        public virtual string TestDir => this.GetProperty<string>("TestDir");

        /// <summary>
        /// Gets directory for files deployed for the test run. Typically a subdirectory of <see cref="TestRunDirectory"/>.
        /// Same as <see cref="DeploymentDirectory"/>. Use that property instead.
        /// </summary>
        public virtual string TestDeploymentDir => this.GetProperty<string>("TestDeploymentDir");

        /// <summary>
        /// Gets directory for test run result files. Typically a subdirectory of <see cref="ResultsDirectory"/>.
        /// Same as <see cref="TestRunResultsDirectory"/>. Use that property for test run result files, or
        /// <see cref="TestResultsDirectory"/> for test-specific result files instead.
        /// </summary>
        public virtual string TestLogsDir => this.GetProperty<string>("TestLogsDir");

        #endregion

        #endregion

        // This property can be useful in attributes derived from ExpectedExceptionBaseAttribute.
        // Those attributes have access to the test context, and provide messages that are included
        // in the test results. Users can benefit from messages that include the fully-qualified
        // class name in addition to the name of the test method currently being executed.

        /// <summary>
        /// Gets the Fully-qualified name of the class containing the test method currently being executed
        /// </summary>
        public virtual string FullyQualifiedTestClassName => this.GetProperty<string>("FullyQualifiedTestClassName");

        /// <summary>
        /// Gets the name of the test method currently being executed
        /// </summary>
        public virtual string TestName => this.GetProperty<string>("TestName");

        /// <summary>
        /// Gets the current test outcome.
        /// </summary>
        public virtual UnitTestOutcome CurrentTestOutcome => UnitTestOutcome.Unknown;

        /// <summary>
        /// Used to write trace messages while the test is running
        /// </summary>
        /// <param name="message">formatted message string</param>
        public abstract void Write(string message);

        /// <summary>
        /// Used to write trace messages while the test is running
        /// </summary>
        /// <param name="format">format string</param>
        /// <param name="args">the arguments</param>
        public abstract void Write(string format, params object[] args);

        /// <summary>
        /// Used to write trace messages while the test is running
        /// </summary>
        /// <param name="message">formatted message string</param>
        public abstract void WriteLine(string message);

        /// <summary>
        /// Used to write trace messages while the test is running
        /// </summary>
        /// <param name="format">format string</param>
        /// <param name="args">the arguments</param>
        public abstract void WriteLine(string format, params object[] args);

        /// <summary>
        /// Adds a file name to the list in TestResult.ResultFileNames
        /// </summary>
        /// <param name="fileName">
        /// The file Name.
        /// </param>
        public abstract void AddResultFile(string fileName);

        /// <summary>
        /// Begins a timer with the specified name
        /// </summary>
        /// <param name="timerName"> Name of the timer.</param>
        public abstract void BeginTimer(string timerName);

        /// <summary>
        /// Ends a timer with the specified name
        /// </summary>
        /// <param name="timerName"> Name of the timer.</param>
        public abstract void EndTimer(string timerName);

        private T GetProperty<T>(string name)
            where T : class
        {
            object o = this.Properties[name];

            // If o has a value, but it's not the right type
            if (o != null && !(o is T))
            {
                Debug.Fail("How did an invalid value get in here?");
                throw new InvalidCastException(string.Format(CultureInfo.CurrentCulture, FrameworkMessages.InvalidPropertyType, name, o.GetType(), typeof(T)));
            }

            return (T)o;
        }
    }
}
