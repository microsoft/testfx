// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;

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
        /// <value></value>
        public abstract IDictionary<string, object> Properties { get; }

        /// <summary>
        /// Gets Fully-qualified name of the class containing the test method currently being executed
        /// </summary>
        /// <remarks>
        /// This property can be useful in attributes derived from ExpectedExceptionBaseAttribute.
        /// Those attributes have access to the test context, and provide messages that are included
        /// in the test results. Users can benefit from messages that include the fully-qualified
        /// class name in addition to the name of the test method currently being executed.
        /// </remarks>
        public virtual string FullyQualifiedTestClassName
        {
            get
            {
                return this.GetProperty<string>("FullyQualifiedTestClassName");
            }
        }

        /// <summary>
        /// Gets the Name of the test method currently being executed
        /// </summary>
        public virtual string TestName
        {
            get
            {
                return this.GetProperty<string>("TestName");
            }
        }

        /// <summary>
        /// Gets the current test outcome.
        /// </summary>
        public virtual UnitTestOutcome CurrentTestOutcome
        {
            get { return UnitTestOutcome.Unknown; }
        }

        private T GetProperty<T>(string name)
            where T : class
        {
            object o;

            if (!this.Properties.TryGetValue(name, out o))
            {
                return null;
            }

            if (o != null && !(o is T))
            {
                // If o has a value, but it's not the right type
                Debug.Assert(false, "How did an invalid value get in here?");
                throw new InvalidCastException(string.Format(CultureInfo.CurrentCulture, FrameworkMessages.InvalidPropertyType, name, o.GetType(), typeof(T)));
            }

            return (T)o;
        }
    }
}
