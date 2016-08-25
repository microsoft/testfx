// ---------------------------------------------------------------------------
// <copyright file="TestContextImpl.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
// <summary>
//   This class contains the implementation of the test context class
// </summary>
// ---------------------------------------------------------------------------

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution
{
    using System.Collections.Generic;
    using System.Diagnostics;

    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

    using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Internal implementation of TestContext exposed to the user.
    /// </summary>
    /// <remarks>
    /// The virtual string properties of the TestContext are retreived from the property dictionary
    /// like GetProperty<string>("TestName") or GetProperty<string>("FullyQualifiedTestClassName");
    /// </remarks>
    public class TestContextImplementation : UTF.TestContext
    {
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
        private TestMethod testMethod;

        /// <summary>
        /// Initializes a new instance of an object that derives from 
        /// the Microsoft.VisualStudio.TestTools.UnitTesting.TestContext class.
        /// </summary>
        /// <param name="testMethod"> The test method. </param>
        /// <param name="properties"> The properties. </param>
        public TestContextImplementation(TestMethod testMethod, IDictionary<string, object> properties)
        {
            Debug.Assert(testMethod != null, "TestMethod is not null");
            Debug.Assert(properties != null, "properties is not null");

            this.testMethod = testMethod;
            this.properties = new Dictionary<string, object>(properties);

            this.InitializeProperties();
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

        // This property can be useful in attributes derived from ExpectedExceptionBaseAttribute.
        // Those attributes have access to the test context, and provide messages that are included
        // in the test results. Users can benefit from messages that include the fully-qualified
        // class name in addition to the name of the test method currently being executed.
        /// <summary>
        /// Fully-qualified name of the class containing the test method currently being executed
        /// </summary>
        public override string FullyQualifiedTestClassName
        {
            get
            {
                return this.GetPropertyValue(TestContextPropertyStrings.FullyQualifiedTestClassName) as string;
            }
        }

        /// <summary>
        /// Name of the test method currently being executed
        /// </summary>
        public override string TestName
        {
            get
            {
                return this.GetPropertyValue(TestContextPropertyStrings.TestName) as string;
            }
        }

        //
        // Summary:
        //     When overridden in a derived class, gets the test properties.
        //
        // Returns:
        //     An System.Collections.IDictionary object that contains key/value pairs that
        //     represent the test properties.
        public override IDictionary<string, object> Properties
        {
            get
            {
                return this.properties as IDictionary<string, object>;
            }
        }

        #endregion

        #region internal methods

        /// <summary>
        /// Set the unit-test outcome
        /// </summary>
        internal void SetOutcome(UTF.UnitTestOutcome outcome)
        {
            this.outcome = outcome;
        }

        /// <summary>
        /// Returns whether properties are present on this test
        /// </summary>
        public bool HasProperties
        {
            get
            {
                return (this.properties != null && this.properties.Count > 0);
            }
        }

        /// <summary>
        /// Returns whether property with parameter name is present or not
        /// </summary>
        internal bool TryGetPropertyValue(string propertyName, out object propertyValue)
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
        internal void AddProperty(string propertyName, string propertyValue)
        {
            if (this.properties == null)
            {
                this.properties = new Dictionary<string, object>();
            }

            this.properties.Add(propertyName, propertyValue);
        }

        #endregion

        /// <summary>
        /// Helper to safely fetch a property value.
        /// </summary>
        /// <param name="propertyName">Property Name</param>
        /// <returns>Property value</returns>
        private object GetPropertyValue(string propertyName)
        {
            object propertyValue = null;
            this.properties.TryGetValue(propertyName, out propertyValue);

            return propertyValue;
        }

        /// <summary>
        /// Helper to initialize the properties.
        /// </summary>
        private void InitializeProperties()
        {
            this.properties[TestContextPropertyStrings.FullyQualifiedTestClassName] = this.testMethod.FullClassName;
            this.properties[TestContextPropertyStrings.TestName] = this.testMethod.Name;
        }
    }

    /// <summary>
    /// Test Context Property Names.
    /// </summary>
    internal static class TestContextPropertyStrings
    {
        public static readonly string FullyQualifiedTestClassName = "FullyQualifiedTestClassName";
        public static readonly string TestName = "TestName";
    }
}
