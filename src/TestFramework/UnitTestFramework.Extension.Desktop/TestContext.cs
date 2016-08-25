// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
    using System;
    using System.Collections;
    using System.Data;
    using System.Data.Common;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;

    /// <summary>
    /// Used to store information that is provided to unit tests.
    /// </summary>
    public abstract class TestContext
    {
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
        public abstract void BeginTimer(string timerName);

        /// <summary>
        /// Ends a timer with the specified name
        /// </summary>
        public abstract void EndTimer(string timerName);

        /// <summary>
        /// Per test properties
        /// </summary>
        /// <value></value>
        public abstract IDictionary Properties { get; }

        /// <summary>
        /// Current data row when test is used for data driven testing.
        /// </summary>
        public abstract DataRow DataRow { get; }

        /// <summary>
        /// Current data connection row when test is used for data driven testing.
        /// </summary>
        public abstract DbConnection DataConnection { get; }

        #region Test run deployment directories

        /// <summary>
        /// Base directory for the test run, under which deployed files and result files are stored.
        /// </summary>
        public virtual string TestRunDirectory => this.GetProperty<string>("TestRunDirectory");

        /// <summary>
        /// Directory for files deployed for the test run. Typically a subdirectory of <see cref="TestRunDirectory"/>.
        /// </summary>
        public virtual string DeploymentDirectory => this.GetProperty<string>("DeploymentDirectory");

        /// <summary>
        /// Base directory for results from the test run. Typically a subdirectory of <see cref="TestRunDirectory"/>.
        /// </summary>
        public virtual string ResultsDirectory => this.GetProperty<string>("ResultsDirectory");

        /// <summary>
        /// Directory for test run result files. Typically a subdirectory of <see cref="ResultsDirectory"/>.
        /// </summary>
        public virtual string TestRunResultsDirectory => this.GetProperty<string>("TestRunResultsDirectory");

        /// <summary>
        /// Directory for test result files.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1702:CompoundWordsShouldBeCasedCorrectly")]
        public virtual string TestResultsDirectory => this.GetProperty<string>("TestResultsDirectory");

        #region Old names, for backwards compatibility

        /// <summary>
        /// Same as <see cref="TestRunDirectory"/>. Use that property instead.
        /// </summary>
        public virtual string TestDir => this.GetProperty<string>("TestDir");

        /// <summary>
        /// Same as <see cref="DeploymentDirectory"/>. Use that property instead.
        /// </summary>
        public virtual string TestDeploymentDir => this.GetProperty<string>("TestDeploymentDir");

        /// <summary>
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
        /// Fully-qualified name of the class containing the test method currently being executed
        /// </summary>
        public virtual string FullyQualifiedTestClassName => this.GetProperty<string>("FullyQualifiedTestClassName");

        /// <summary>
        /// Name of the test method currently being executed
        /// </summary>
        public virtual string TestName => this.GetProperty<string>("TestName");

        public virtual UnitTestOutcome CurrentTestOutcome => UnitTestOutcome.Unknown;

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
