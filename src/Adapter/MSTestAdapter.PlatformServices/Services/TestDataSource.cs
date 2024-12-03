// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Globalization;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Data;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

using ITestDataSource = Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ITestDataSource;
using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

/// <summary>
/// The platform service that provides values from data source when data driven tests are run.
/// </summary>
/// <remarks>
/// NOTE NOTE NOTE: This platform service refers to the inbox UTF extension assembly for UTF.TestContext which can only be loadable inside of the app domain that discovers/runs
/// the tests since it can only be found at the test output directory. DO NOT call into this platform service outside of the appdomain context if you do not want to hit
/// a ReflectionTypeLoadException.
/// </remarks>
#if NET6_0_OR_GREATER
[Obsolete(Constants.PublicTypeObsoleteMessage, DiagnosticId = "MSTESTOBS")]
#else
[Obsolete(Constants.PublicTypeObsoleteMessage)]
#endif
public class TestDataSource : ITestDataSource
{
#if NETFRAMEWORK
    public IEnumerable<object>? GetData(UTF.ITestMethod testMethodInfo, ITestContext testContext)
#else
    IEnumerable<object>? ITestDataSource.GetData(UTF.ITestMethod testMethodInfo, ITestContext testContext)
#endif
    {
#if NETFRAMEWORK
        // Figure out where (as well as the current directory) we could look for data files
        // for unit tests this means looking at the location of the test itself
        List<string> dataFolders =
        [
            Path.GetDirectoryName(new Uri(testMethodInfo.MethodInfo.Module.Assembly.CodeBase).LocalPath),
        ];

        List<UTF.TestResult> dataRowResults = [];

        // Connect to data source.
        TestDataConnectionFactory factory = new();

        GetConnectionProperties(testMethodInfo.GetAttributes<UTF.DataSourceAttribute>(false)[0], out string providerNameInvariant, out string? connectionString, out string? tableName, out UTF.DataAccessMethod dataAccessMethod);

        try
        {
            using TestDataConnection connection = factory.Create(providerNameInvariant, connectionString!, dataFolders);
            DataTable? table = connection.ReadTable(tableName!, null);
            DebugEx.Assert(table != null, "Table should not be null");
            DataRow[] rows = table!.Select();
            DebugEx.Assert(rows != null, "rows should not be null.");

            // check for row length is 0
            if (rows.Length == 0)
            {
                return null;
            }

            IEnumerable<int> permutation = GetPermutation(dataAccessMethod, rows.Length);

            object[] rowsAfterPermutation = new object[rows.Length];
            int index = 0;
            foreach (int rowIndex in permutation)
            {
                rowsAfterPermutation[index++] = rows[rowIndex];
            }

            testContext.SetDataConnection(connection.Connection);
            return rowsAfterPermutation;
        }
        catch (Exception ex)
        {
            string message = ex.GetExceptionMessage();

            // TODO: Change exception type to more specific one.
#pragma warning disable CA2201 // Do not raise reserved exception types
            throw new Exception(string.Format(CultureInfo.CurrentCulture, Resource.UTA_ErrorDataConnectionFailed, message), ex);
#pragma warning restore CA2201 // Do not raise reserved exception types
        }
#else
        return null;
#endif
    }

#if NETFRAMEWORK
    /// <summary>
    /// Get permutations for data row access.
    /// </summary>
    /// <param name="dataAccessMethod">The data access method.</param>
    /// <param name="length">Number of permutations.</param>
    /// <returns>Permutations.</returns>
    private static IEnumerable<int> GetPermutation(UTF.DataAccessMethod dataAccessMethod, int length)
    {
        switch (dataAccessMethod)
        {
            case DataAccessMethod.Sequential:
                return new SequentialIntPermutation(length);

            case DataAccessMethod.Random:
                return new RandomIntPermutation(length);

            default:
                Debug.Fail("Unknown DataAccessMethod: " + dataAccessMethod);
                return new SequentialIntPermutation(length);
        }
    }

    /// <summary>
    /// Get connection property based on DataSourceAttribute. If its in config file then read it from config.
    /// </summary>
    /// <param name="dataSourceAttribute">The dataSourceAttribute.</param>
    /// <param name="providerNameInvariant">The provider name.</param>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="dataAccessMethod">The data access method.</param>
    private static void GetConnectionProperties(UTF.DataSourceAttribute dataSourceAttribute, out string providerNameInvariant,
        out string? connectionString, out string? tableName, out UTF.DataAccessMethod dataAccessMethod)
    {
        if (StringEx.IsNullOrEmpty(dataSourceAttribute.DataSourceSettingName))
        {
            providerNameInvariant = dataSourceAttribute.ProviderInvariantName;
            connectionString = dataSourceAttribute.ConnectionString;
            tableName = dataSourceAttribute.TableName;
            dataAccessMethod = dataSourceAttribute.DataAccessMethod;
            return;
        }

        UTF.DataSourceElement element = TestConfiguration.ConfigurationSection.DataSources[dataSourceAttribute.DataSourceSettingName]
#pragma warning disable CA2201 // Do not raise reserved exception types
            ?? throw new Exception(string.Format(CultureInfo.CurrentCulture, Resource.UTA_DataSourceConfigurationSectionMissing, dataSourceAttribute.DataSourceSettingName));
#pragma warning restore CA2201 // Do not raise reserved exception types
        providerNameInvariant = ConfigurationManager.ConnectionStrings[element.ConnectionString].ProviderName;
        connectionString = ConfigurationManager.ConnectionStrings[element.ConnectionString].ConnectionString;
        tableName = element.DataTableName;
        dataAccessMethod = EnumPolyfill.Parse<DataAccessMethod>(element.DataAccessMethod);
    }
#endif
}
