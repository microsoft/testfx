// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Data;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Data;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Extensions;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

    using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable SA1649 // SA1649FileNameMustMatchTypeName

    /// <summary>
    /// The platform service that provides values from data source when data driven tests are run.
    /// </summary>
    /// <remarks>
    /// NOTE NOTE NOTE: This platform service refers to the inbox UTF extension assembly for UTF.ITestMethod which can only be loadable inside of the app domain that discovers/runs
    /// the tests since it can only be found at the test output directory. DO NOT call into this platform service outside of the appdomain context if you do not want to hit
    /// a ReflectionTypeLoadException.
    /// </remarks>
    public class TestDataSource : ITestDataSource
    {
        public IEnumerable<object> GetData(UTF.ITestMethod testMethodInfo, ITestContext testContext)
        {
            // Figure out where (as well as the current directory) we could look for data files
            // for unit tests this means looking at the location of the test itself
            List<string> dataFolders = new List<string>();
            dataFolders.Add(Path.GetDirectoryName(new Uri(testMethodInfo.MethodInfo.Module.Assembly.CodeBase).LocalPath));

            List<UTF.TestResult> dataRowResults = new List<UTF.TestResult>();

            // Connect to data source.
            TestDataConnectionFactory factory = new TestDataConnectionFactory();

            string providerNameInvariant;
            string connectionString;
            string tableName;
            UTF.DataAccessMethod dataAccessMethod;

            try
            {
                this.GetConnectionProperties(testMethodInfo.GetAttributes<UTF.DataSourceAttribute>(false)[0], out providerNameInvariant, out connectionString, out tableName, out dataAccessMethod);
            }
            catch (Exception ex)
            {
                throw ex;
            }

            try
            {
                using (TestDataConnection connection = factory.Create(providerNameInvariant, connectionString, dataFolders))
                {
                    DataTable table = connection.ReadTable(tableName, null);
                    DataRow[] rows = table.Select();
                    Debug.Assert(rows != null, "rows should not be null.");

                    // check for row length is 0
                    if (rows.Length == 0)
                    {
                        return null;
                    }

                    IEnumerable<int> permutation = this.GetPermutation(dataAccessMethod, rows.Length);

                    object[] rowsAfterPermutation = new object[rows.Length];
                    int index = 0;
                    foreach (int rowIndex in permutation)
                    {
                        rowsAfterPermutation[index++] = rows[rowIndex];
                    }

                    testContext.SetDataConnection(connection.Connection);
                    return rowsAfterPermutation;
                }
            }
            catch (Exception ex)
            {
                string message = ExceptionExtensions.GetExceptionMessage(ex);
                throw new Exception(string.Format(CultureInfo.CurrentCulture, Resource.UTA_ErrorDataConnectionFailed, ex.Message), ex);
            }
        }

        /// <summary>
        /// Get permutations for data row access
        /// </summary>
        /// <param name="dataAccessMethod">The data access method.</param>
        /// <param name="length">Number of permutations.</param>
        /// <returns>Permutations.</returns>
        private IEnumerable<int> GetPermutation(UTF.DataAccessMethod dataAccessMethod, int length)
        {
            switch (dataAccessMethod)
            {
                case UTF.DataAccessMethod.Sequential:
                    return new SequentialIntPermutation(length);

                case UTF.DataAccessMethod.Random:
                    return new RandomIntPermutation(length);

                default:
                    Debug.Fail("Unknown DataAccessMehtod: " + dataAccessMethod);
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
        private void GetConnectionProperties(UTF.DataSourceAttribute dataSourceAttribute, out string providerNameInvariant, out string connectionString, out string tableName, out UTF.DataAccessMethod dataAccessMethod)
        {
            if (string.IsNullOrEmpty(dataSourceAttribute.DataSourceSettingName) == false)
            {
                UTF.DataSourceElement elem = UTF.TestConfiguration.ConfigurationSection.DataSources[dataSourceAttribute.DataSourceSettingName];
                if (elem == null)
                {
                    throw new Exception(string.Format(CultureInfo.CurrentCulture, Resource.UTA_DataSourceConfigurationSectionMissing, dataSourceAttribute.DataSourceSettingName));
                }

                providerNameInvariant = ConfigurationManager.ConnectionStrings[elem.ConnectionString].ProviderName;
                connectionString = ConfigurationManager.ConnectionStrings[elem.ConnectionString].ConnectionString;
                tableName = elem.DataTableName;
                dataAccessMethod = (UTF.DataAccessMethod)Enum.Parse(typeof(UTF.DataAccessMethod), elem.DataAccessMethod);
            }
            else
            {
                providerNameInvariant = dataSourceAttribute.ProviderInvariantName;
                connectionString = dataSourceAttribute.ConnectionString;
                tableName = dataSourceAttribute.TableName;
                dataAccessMethod = dataSourceAttribute.DataAccessMethod;
            }
        }
    }

#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName
}
