// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
    using global::System;
    using global::System.Collections;
    using global::System.Collections.Generic;
    using global::System.Globalization;
    using global::System.Threading;

    /// <summary>
    /// TestContext class. This class should be fully abstract and not contain any
    /// members. The adapter will implement the members. Users in the framework should
    /// only access this via a well-defined interface.
    /// </summary>
    public abstract class TestContext
    {
        /// <summary>
        /// Gets test properties for a test.
        /// </summary>
        public abstract IDictionary Properties { get; }

        /// <summary>
        /// Gets or sets the cancellation token source. This token source is cancelled when test times out. Also when explicitly cancelled the test will be aborted
        /// </summary>
        public virtual CancellationTokenSource CancellationTokenSource { get; protected set; }

        /// <summary>
        /// Gets Fully-qualified name of the class containing the test method currently being executed
        /// </summary>
        /// <remarks>
        /// This property can be useful in attributes derived from ExpectedExceptionBaseAttribute.
        /// Those attributes have access to the test context, and provide messages that are included
        /// in the test results. Users can benefit from messages that include the fully-qualified
        /// class name in addition to the name of the test method currently being executed.
        /// </remarks>
        public virtual string FullyQualifiedTestClassName => this.GetProperty<string>("FullyQualifiedTestClassName");

        /// <summary>
        /// Gets the fully specified type name metadata format.
        /// </summary>
        public virtual string ManagedType => this.GetProperty<string>(nameof(this.ManagedType));

        /// <summary>
        /// Gets the fully specified method name metadata format.
        /// </summary>
        public virtual string ManagedMethod => this.GetProperty<string>(nameof(this.ManagedMethod));

        /// <summary>
        /// Gets the Name of the test method currently being executed
        /// </summary>
        public virtual string TestName => this.GetProperty<string>("TestName");

        /// <summary>
        /// Gets the current test outcome.
        /// </summary>
        public virtual UnitTestOutcome CurrentTestOutcome => UnitTestOutcome.Unknown;

        /// <summary>
        /// Adds a file name to the list in TestResult.ResultFileNames.
        /// </summary>
        /// <param name="fileName">
        /// The file name.
        /// </param>
        public abstract void AddResultFile(string fileName);

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

        private T GetProperty<T>(string name)
            where T : class
        {
            if (!((IDictionary<string, object>)this.Properties).TryGetValue(name, out object propertyValue))
            {
                return null;
            }

            if (propertyValue != null && !(propertyValue is T))
            {
                // If o has a value, but it's not the right type
                throw new InvalidCastException(string.Format(CultureInfo.CurrentCulture, FrameworkMessages.InvalidPropertyType, name, propertyValue.GetType(), typeof(T)));
            }

            return (T)propertyValue;
        }
    }
}
