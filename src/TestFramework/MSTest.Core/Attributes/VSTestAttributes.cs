// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;

#pragma warning disable SA1402 // FileMayOnlyContainASingleType
#pragma warning disable SA1649 // SA1649FileNameMustMatchTypeName

    /// <summary>
    /// Enumeration for timeouts, that can be used with the <see cref="TimeoutAttribute"/> class.
    /// The type of the enumeration must match
    /// </summary>
    [SuppressMessage("Microsoft.Design", "CA1008:EnumsShouldHaveZeroValue", Justification = "Compat reasons")]
    public enum TestTimeout
    {
        /// <summary>
        /// The infinite.
        /// </summary>
        Infinite = int.MaxValue
    }

    /// <summary>
    /// Enumeration for inheritance behavior, that can be used with both the <see cref="ClassInitializeAttribute"/> class
    /// and <see cref="ClassCleanupAttribute"/> class.
    /// Defines the behavior of the ClassInitialize and ClassCleanup methods of base classes.
    /// The type of the enumeration must match
    /// </summary>
    public enum InheritanceBehavior
    {
        /// <summary>
        /// None.
        /// </summary>
        None,

        /// <summary>
        /// Before each derived class.
        /// </summary>
        BeforeEachDerivedClass
    }

    /// <summary>
    /// The test class attribute.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class TestClassAttribute : Attribute
    {
        /// <summary>
        /// Gets a test method attribute that enables running this test.
        /// </summary>
        /// <param name="testMethodAttribute">The test method attribute instance defined on this method.</param>
        /// <returns>The <see cref="TestMethodAttribute"/> to be used to run this test.</returns>
        /// <remarks>Extensions can override this method to customize how all methods in a class are run.</remarks>
        public virtual TestMethodAttribute GetTestMethodAttribute(TestMethodAttribute testMethodAttribute)
        {
            // If TestMethod is not extended by derived class then return back the original TestMethodAttribute
            return testMethodAttribute;
        }
    }

    /// <summary>
    /// The test method attribute.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class TestMethodAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TestMethodAttribute"/> class.
        /// </summary>
        public TestMethodAttribute()
        : this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TestMethodAttribute"/> class.
        /// </summary>
        /// <param name="displayName">
        /// Message specifies reason for ignoring.
        /// </param>
        public TestMethodAttribute(string displayName)
        {
            this.DisplayName = displayName;
        }

        /// <summary>
        /// Gets display Name for the Test Window
        /// </summary>
        public string DisplayName { get; private set; }

        /// <summary>
        /// Executes a test method.
        /// </summary>
        /// <param name="testMethod">The test method to execute.</param>
        /// <returns>An array of TestResult objects that represent the outcome(s) of the test.</returns>
        /// <remarks>Extensions can override this method to customize running a TestMethod.</remarks>
        public virtual TestResult[] Execute(ITestMethod testMethod)
        {
            return new TestResult[] { testMethod.Invoke(null) };
        }
    }

    /// <summary>
    /// Attribute for data driven test where data can be specified inline.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class DataTestMethodAttribute : TestMethodAttribute
    {
    }

    /// <summary>
    /// The test initialize attribute.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class TestInitializeAttribute : Attribute
    {
    }

    /// <summary>
    /// The test cleanup attribute.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class TestCleanupAttribute : Attribute
    {
    }

    /// <summary>
    /// The ignore attribute.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class IgnoreAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IgnoreAttribute"/> class.
        /// </summary>
        public IgnoreAttribute()
            : this(string.Empty)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IgnoreAttribute"/> class.
        /// </summary>
        /// <param name="message">
        /// Message specifies reason for ignoring.
        /// </param>
        public IgnoreAttribute(string message)
        {
            this.IgnoreMessage = message;
        }

        /// <summary>
        /// Gets the owner.
        /// </summary>
        public string IgnoreMessage { get; private set; }
    }

    /// <summary>
    /// The test property attribute.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class TestPropertyAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TestPropertyAttribute"/> class.
        /// </summary>
        /// <param name="name">
        /// The name.
        /// </param>
        /// <param name="value">
        /// The value.
        /// </param>
        public TestPropertyAttribute(string name, string value)
        {
            // NOTE : DONT THROW EXCEPTIONS FROM HERE IT WILL CRASH GetCustomAttributes() call
            this.Name = name;
            this.Value = value;
        }

        /// <summary>
        /// Gets the name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the value.
        /// </summary>
        public string Value { get; }
    }

    /// <summary>
    /// The class initialize attribute.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class ClassInitializeAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ClassInitializeAttribute"/> class.
        /// ClassInitializeAttribute
        /// </summary>
        public ClassInitializeAttribute()
        {
            this.InheritanceBehavior = InheritanceBehavior.None;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClassInitializeAttribute"/> class.
        /// ClassInitializeAttribute
        /// </summary>
        /// <param name="inheritanceBehavior">
        /// Specifies the ClassInitialize Inheritance Behavior
        /// </param>
        public ClassInitializeAttribute(InheritanceBehavior inheritanceBehavior)
        {
            this.InheritanceBehavior = inheritanceBehavior;
        }

        /// <summary>
        /// Gets the Inheritance Behavior
        /// </summary>
        public InheritanceBehavior InheritanceBehavior { get; private set; }
    }

    /// <summary>
    /// The class cleanup attribute.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class ClassCleanupAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ClassCleanupAttribute"/> class.
        /// ClassCleanupAttribute
        /// </summary>
        public ClassCleanupAttribute()
        {
            this.InheritanceBehavior = InheritanceBehavior.None;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClassCleanupAttribute"/> class.
        /// ClassCleanupAttribute
        /// </summary>
        /// <param name="inheritanceBehavior">
        /// Specifies the ClassCleanup Inheritance Behavior
        /// </param>
        public ClassCleanupAttribute(InheritanceBehavior inheritanceBehavior)
        {
            this.InheritanceBehavior = inheritanceBehavior;
        }

        /// <summary>
        /// Gets the Inheritance Behavior
        /// </summary>
        public InheritanceBehavior InheritanceBehavior { get; private set; }
    }

    /// <summary>
    /// The assembly initialize attribute.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class AssemblyInitializeAttribute : Attribute
    {
    }

    /// <summary>
    /// The assembly cleanup attribute.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class AssemblyCleanupAttribute : Attribute
    {
    }

    /// <summary>
    /// Test Owner
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class OwnerAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OwnerAttribute"/> class.
        /// </summary>
        /// <param name="owner">
        /// The owner.
        /// </param>
        public OwnerAttribute(string owner)
        {
            this.Owner = owner;
        }

        /// <summary>
        /// Gets the owner.
        /// </summary>
        public string Owner { get; }
    }

    /// <summary>
    /// Priority attribute; used to specify the priority of a unit test.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class PriorityAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PriorityAttribute"/> class.
        /// </summary>
        /// <param name="priority">
        /// The priority.
        /// </param>
        public PriorityAttribute(int priority)
        {
            this.Priority = priority;
        }

        /// <summary>
        /// Gets the priority.
        /// </summary>
        public int Priority { get; }
    }

    /// <summary>
    /// Description of the test
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class DescriptionAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DescriptionAttribute"/> class to describe a test.
        /// </summary>
        /// <param name="description">The description.</param>
        public DescriptionAttribute(string description)
        {
            this.Description = description;
        }

        /// <summary>
        /// Gets the description of a test.
        /// </summary>
        public string Description { get; private set; }
    }

    /// <summary>
    /// CSS Project Structure URI
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class CssProjectStructureAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CssProjectStructureAttribute"/> class for CSS Project Structure URI.
        /// </summary>
        /// <param name="cssProjectStructure">The CSS Project Structure URI.</param>
        public CssProjectStructureAttribute(string cssProjectStructure)
        {
            this.CssProjectStructure = cssProjectStructure;
        }

        /// <summary>
        /// Gets the CSS Project Structure URI.
        /// </summary>
        public string CssProjectStructure { get; private set; }
    }

    /// <summary>
    /// CSS Iteration URI
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class CssIterationAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CssIterationAttribute"/> class for CSS Iteration URI.
        /// </summary>
        /// <param name="cssIteration">The CSS Iteration URI.</param>
        public CssIterationAttribute(string cssIteration)
        {
            this.CssIteration = cssIteration;
        }

        /// <summary>
        /// Gets the CSS Iteration URI.
        /// </summary>
        public string CssIteration { get; private set; }
    }

    /// <summary>
    /// WorkItem attribute; used to specify a work item associated with this test.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class WorkItemAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WorkItemAttribute"/> class for the WorkItem Attribute.
        /// </summary>
        /// <param name="id">The Id to a work item.</param>
        public WorkItemAttribute(int id)
        {
            this.Id = id;
        }

        /// <summary>
        /// Gets the Id to a workitem associated.
        /// </summary>
        public int Id { get; private set; }
    }

    /// <summary>
    /// Timeout attribute; used to specify the timeout of a unit test.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class TimeoutAttribute : Attribute
    {
        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="TimeoutAttribute"/> class.
        /// </summary>
        /// <param name="timeout">
        /// The timeout in milliseconds.
        /// </param>
        public TimeoutAttribute(int timeout)
        {
            this.Timeout = timeout;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TimeoutAttribute"/> class with a preset timeout
        /// </summary>
        /// <param name="timeout">
        /// The timeout
        /// </param>
        public TimeoutAttribute(TestTimeout timeout)
        {
            this.Timeout = (int)timeout;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the timeout in milliseconds.
        /// </summary>
        public int Timeout { get; }

        #endregion
    }

    /// <summary>
    /// TestResult object to be returned to adapter.
    /// </summary>
    public class TestResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TestResult"/> class.
        /// </summary>
        public TestResult()
        {
            this.DatarowIndex = -1;
        }

        /// <summary>
        /// Gets or sets the display name of the result. Useful when returning multiple results.
        /// If null then Method name is used as DisplayName.
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Gets or sets the outcome of the test execution.
        /// </summary>
        public UnitTestOutcome Outcome { get; set; }

        /// <summary>
        /// Gets or sets the exception thrown when test is failed.
        /// </summary>
        public Exception TestFailureException { get; set; }

        /// <summary>
        /// Gets or sets the output of the message logged by test code.
        /// </summary>
        public string LogOutput { get; set; }

        /// <summary>
        /// Gets or sets the output of the message logged by test code.
        /// </summary>
        public string LogError { get; set; }

        /// <summary>
        /// Gets or sets the debug traces by test code.
        /// </summary>
        public string DebugTrace { get; set; }

        /// <summary>
        /// Gets or sets the debug traces by test code.
        /// </summary>
        public string TestContextMessages { get; set; }

        /// <summary>
        /// Gets or sets the execution id of the result.
        /// </summary>
        public Guid ExecutionId { get; set; }

        /// <summary>
        /// Gets or sets the parent execution id of the result.
        /// </summary>
        public Guid ParentExecId { get; set; }

        /// <summary>
        /// Gets or sets the inner results count of the result.
        /// </summary>
        public int InnerResultsCount { get; set; }

        /// <summary>
        /// Gets or sets the duration of test execution.
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Gets or sets the data row index in data source. Set only for results of individual
        /// run of data row of a data driven test.
        /// </summary>
        public int DatarowIndex { get; set; }

        /// <summary>
        /// Gets or sets the return value of the test method. (Currently null always).
        /// </summary>
        public object ReturnValue { get; set; }

        /// <summary>
        /// Gets or sets the result files attached by the test.
        /// </summary>
        public IList<string> ResultFiles { get; set; }
    }

    /// <summary>
    /// Specifies connection string, table name and row access method for data driven testing.
    /// </summary>
    /// <example>
    /// [DataSource("Provider=SQLOLEDB.1;Data Source=source;Integrated Security=SSPI;Initial Catalog=EqtCoverage;Persist Security Info=False", "MyTable")]
    /// [DataSource("dataSourceNameFromConfigFile")]
    /// </example>
    [SuppressMessage("Microsoft.Design", "CA1019:DefineAccessorsForAttributeArguments", Justification = "Compat")]
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class DataSourceAttribute : Attribute
    {
        // DefaultProviderName needs not to be constant so that clients do not need
        // to recompile if the value changes.

        /// <summary>
        /// The default provider name for DataSource.
        /// </summary>
        [SuppressMessage("Microsoft.Performance", "CA1802:UseLiteralsWhereAppropriate", Justification = "Compat")]
        public static readonly string DefaultProviderName = "System.Data.OleDb";

        /// <summary>
        /// The default data access method.
        /// </summary>
        public static readonly DataAccessMethod DefaultDataAccessMethod = DataAccessMethod.Random;

        /// <summary>
        /// Initializes a new instance of the <see cref="DataSourceAttribute"/> class. This instance will be initialized with a data provider, connection string, data table and data access method to access the data source.
        /// </summary>
        /// <param name="providerInvariantName">Invariant data provider name, such as System.Data.SqlClient</param>
        /// <param name="connectionString">
        /// Data provider specific connection string.
        /// WARNING: The connection string can contain sensitive data (for example, a password).
        /// The connection string is stored in plain text in source code and in the compiled assembly.
        /// Restrict access to the source code and assembly to protect this sensitive information.
        /// </param>
        /// <param name="tableName">The name of the data table.</param>
        /// <param name="dataAccessMethod">Specifies the order to access data.</param>
        public DataSourceAttribute(string providerInvariantName, string connectionString, string tableName, DataAccessMethod dataAccessMethod)
        {
            this.ProviderInvariantName = providerInvariantName;
            this.ConnectionString = connectionString;
            this.TableName = tableName;
            this.DataAccessMethod = dataAccessMethod;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DataSourceAttribute"/> class.This instance will be initialized with a connection string and table name.
        /// Specify connection string and data table to access OLEDB data source.
        /// </summary>
        /// <param name="connectionString">
        /// Data provider specific connection string.
        /// WARNING: The connection string can contain sensitive data (for example, a password).
        /// The connection string is stored in plain text in source code and in the compiled assembly.
        /// Restrict access to the source code and assembly to protect this sensitive information.
        /// </param>
        /// <param name="tableName">The name of the data table.</param>
        public DataSourceAttribute(string connectionString, string tableName)
            : this(DefaultProviderName, connectionString, tableName, DefaultDataAccessMethod)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DataSourceAttribute"/> class.  This instance will be initialized with a data provider and connection string associated with the setting name.
        /// </summary>
        /// <param name="dataSourceSettingName">The name of a data source found in the &lt;microsoft.visualstudio.qualitytools&gt; section in the app.config file.</param>
        public DataSourceAttribute(string dataSourceSettingName)
        {
            this.DataSourceSettingName = dataSourceSettingName;
        }

        // Different providers use dfferent connection strings and provider itself is a part of connection string.

        /// <summary>
        /// Gets a value representing the data provider of the data source.
        /// </summary>
        /// <returns>
        /// The data provider name. If a data provider was not designated at object initialization, the default provider of System.Data.OleDb will be returned.
        /// </returns>
        public string ProviderInvariantName { get; } = DefaultProviderName;

        /// <summary>
        /// Gets a value representing the connection string for the data source.
        /// </summary>
        public string ConnectionString { get; }

        /// <summary>
        /// Gets a value indicating the table name providing data.
        /// </summary>
        public string TableName { get; }

        /// <summary>
        /// Gets the method used to access the data source.
        /// </summary>
        ///
        /// <returns>
        /// One of the <see cref="T:Microsoft.VisualStudio.TestTools.UnitTesting.DataAccessMethod"/> values. If the <see cref="DataSourceAttribute"/> is not initialized, this will return the default value <see cref="F:Microsoft.VisualStudio.TestTools.UnitTesting.DataAccessMethod.Random"/>.
        /// </returns>
        public DataAccessMethod DataAccessMethod { get; }

        /// <summary>
        /// Gets the name of a data source found in the &lt;microsoft.visualstudio.qualitytools&gt; section in the app.config file.
        /// </summary>
        public string DataSourceSettingName { get; }
    }

#pragma warning restore SA1402 // FileMayOnlyContainASingleType
#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName
}
