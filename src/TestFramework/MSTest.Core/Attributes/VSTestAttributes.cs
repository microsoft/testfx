// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;

#pragma warning disable SA1402 // FileMayOnlyContainASingleType
#pragma warning disable SA1649 // SA1649FileNameMustMatchTypeName

    /// <summary>
    /// Enumeration for timeouts, that can be used with the <see cref="TimeoutAttribute"/> class.
    /// The type of the enumeration must match
    /// </summary>
    [SuppressMessage("Microsoft.Design", "CA1008:EnumsShouldHaveZeroValue", Justification ="Compat reasons")]
    public enum TestTimeout
    {
        /// <summary>
        /// The infinite.
        /// </summary>
        Infinite = int.MaxValue
    }

    /// <summary>
    /// The test class attribute.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class TestClassAttribute : Attribute
    {
        /// <summary>
        /// The get test method attribute.
        /// </summary>
        /// <param name="testMethodAttribute">
        /// The test method attribute.
        /// </param>
        /// <returns>
        /// The <see cref="TestMethodAttribute"/>.
        /// </returns>
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
        /// Extension point for UTF Extension to execute tests.
        /// </summary>
        /// <param name="testMethod"> TestMethod for execution. </param>
        /// <returns> Test Results </returns>
        public virtual TestResult[] Execute(ITestMethod testMethod)
        {
            DataRowAttribute[] dataRows = testMethod.GetAttributes<DataRowAttribute>(false);

            if (dataRows == null || dataRows.Length == 0)
            {
                return new TestResult[] { testMethod.Invoke(null) };
            }

            return DataTestMethodAttribute.RunDataDrivenTest(testMethod, dataRows);
        }
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
    }

    /// <summary>
    /// The class cleanup attribute.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class ClassCleanupAttribute : Attribute
    {
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

    #region Description attributes

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
    public sealed class DescriptionAttribute : TestPropertyAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DescriptionAttribute"/> class to describe a test.
        /// </summary>
        /// <param name="description">The description.</param>
        public DescriptionAttribute(string description)
            : base("Description", description)
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
    public sealed class CssProjectStructureAttribute : TestPropertyAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CssProjectStructureAttribute"/> class for CSS Project Structure URI.
        /// </summary>
        /// <param name="cssProjectStructure">The CSS Project Structure URI.</param>
        public CssProjectStructureAttribute(string cssProjectStructure)
            : base("CssProjectStructure", cssProjectStructure)
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
    public sealed class CssIterationAttribute : TestPropertyAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CssIterationAttribute"/> class for CSS Iteration URI.
        /// </summary>
        /// <param name="cssIteration">The CSS Iteration URI.</param>
        public CssIterationAttribute(string cssIteration)
            : base("CssIteration", cssIteration)
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
    public sealed class WorkItemAttribute : TestPropertyAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WorkItemAttribute"/> class for the WorkItem Attribute.
        /// </summary>
        /// <param name="id">The Id to a work item.</param>
        public WorkItemAttribute(int id)
            : base("WorkItem", id.ToString(CultureInfo.CurrentCulture))
        {
            this.Id = id;
        }

        /// <summary>
        /// Gets the Id to a workitem associated.
        /// </summary>
        public int Id { get; private set; }
    }

    #endregion

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
        /// The timeout.
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
        /// Gets the timeout.
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
    /// Used to specify deployment item (file or directory) for per-test deployment.
    /// Can be specified on test class or test method.
    /// Can have multiple instances of the attribute to specify more than one item.
    /// The item path can be absolute or relative, if relative, it is relative to RunConfig.RelativePathRoot.
    /// </summary>
    /// <example>
    /// [DeploymentItem("file1.xml")]
    /// [DeploymentItem("file2.xml", "DataFiles")]
    /// [DeploymentItem("bin\Debug")]
    /// </example>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
    public sealed class DeploymentItemAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DeploymentItemAttribute"/> class.
        /// </summary>
        /// <param name="path">The file or directory to deploy. The path is relative to the build output directory. The item will be copied to the same directory as the deployed test assemblies.</param>
        public DeploymentItemAttribute(string path)
        {
            this.Path = path;
            this.OutputDirectory = string.Empty;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DeploymentItemAttribute"/> class
        /// </summary>
        /// <param name="path">The relative or absolute path to the file or directory to deploy. The path is relative to the build output directory. The item will be copied to the same directory as the deployed test assemblies.</param>
        /// <param name="outputDirectory">The path of the directory to which the items are to be copied. It can be either absolute or relative to the deployment directory. All files and directories identified by <paramref name="path"/> will be copied to this directory.</param>
        public DeploymentItemAttribute(string path, string outputDirectory)
        {
            this.Path = path;
            this.OutputDirectory = outputDirectory;
        }

        /// <summary>
        /// Gets the path of the source file or folder to be copied.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Gets the path of the directory to which the item is copied.
        /// </summary>
        public string OutputDirectory { get; }
    }

    /// <summary>
    /// Specifies connection string, table name and row access method for data driven testing.
    /// </summary>
    /// <example>
    /// [DataSource("Provider=SQLOLEDB.1;Data Source=source;Integrated Security=SSPI;Initial Catalog=EqtCoverage;Persist Security Info=False", "MyTable")]
    /// [DataSource("dataSourceNameFromConfigFile")]
    /// </example>
    [SuppressMessage("Microsoft.Design", "CA1019:DefineAccessorsForAttributeArguments", Justification ="Compat")]
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class DataSourceAttribute : Attribute
    {
        // DefaultProviderName needs not to be constant so that clients do not need
        // to recompile if the value changes.

        /// <summary>
        /// The default provider name for DataSource.
        /// </summary>
        [SuppressMessage("Microsoft.Performance", "CA1802:UseLiteralsWhereAppropriate", Justification ="Compat")]
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
