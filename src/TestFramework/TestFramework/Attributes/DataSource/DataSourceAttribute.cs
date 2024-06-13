// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Specifies connection string, table name and row access method for data driven testing.
/// </summary>
/// <example>
/// [DataSource("Provider=SQLOLEDB.1;Data Source=source;Integrated Security=SSPI;Initial Catalog=EqtCoverage;Persist Security Info=False", "MyTable")]
/// [DataSource("dataSourceNameFromConfigFile")].
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
    /// <param name="providerInvariantName">Invariant data provider name, such as System.Data.SqlClient.</param>
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
        ProviderInvariantName = providerInvariantName;
        ConnectionString = connectionString;
        TableName = tableName;
        DataAccessMethod = dataAccessMethod;
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
        DataSourceSettingName = dataSourceSettingName;
    }

    // Different providers use different connection strings and provider itself is a part of connection string.

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
    public string? ConnectionString { get; }

    /// <summary>
    /// Gets a value indicating the table name providing data.
    /// </summary>
    public string? TableName { get; }

    /// <summary>
    /// Gets the method used to access the data source.
    /// </summary>
    ///
    /// <returns>
    /// One of the <see cref="Microsoft.VisualStudio.TestTools.UnitTesting.DataAccessMethod"/> values. If the <see cref="DataSourceAttribute"/> is not initialized, this will return the default value <see cref="Microsoft.VisualStudio.TestTools.UnitTesting.DataAccessMethod.Random"/>.
    /// </returns>
    public DataAccessMethod DataAccessMethod { get; }

    /// <summary>
    /// Gets the name of a data source found in the &lt;microsoft.visualstudio.qualitytools&gt; section in the app.config file.
    /// </summary>
    public string? DataSourceSettingName { get; }
}
