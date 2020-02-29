// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Data
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;

    /// <summary>
    /// Defines a class that creates TestDataConnection instances to connect to data sources.
    /// </summary>
    internal class TestDataConnectionFactory
    {
        // These are not "real" providers, but are recognized by the test runtime
        private const string CsvProvider = "Microsoft.VisualStudio.TestTools.DataSource.CSV";
        private const string XmlProvider = "Microsoft.VisualStudio.TestTools.DataSource.XML";

        /// <summary>
        /// Test Specific Providers: maps provider name to provider factory that we lookup prior to using (by default) SqlTestDataConnection.
        /// Notes
        /// - the key (provider name is case-sensitive).
        /// - other providers can be registered using RegisterProvider (per app domain).
        /// </summary>
        private static Dictionary<string, TestDataConnectionFactory> specializedProviders = new Dictionary<string, TestDataConnectionFactory>
        {
            // The XML case is quite unlike all others, as there is no real DB connection at all!
            { XmlProvider, new XmlTestDataConnectionFactory() },

            // The CSV case does use a database connection, but it is hidden, and schema
            // queries are highly specialized
            { CsvProvider, new CsvTestDataConnectionFactory() },
        };

        /// <summary>
        /// Construct a wrapper for a database connection, what is actually returned indirectly depends
        /// on the invariantProviderName, and the specific call knows how to deal with database variations
        /// </summary>
        /// <param name="invariantProviderName">The provider name.</param>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="dataFolders">null, or a list of locations to check when fixing up connection string</param>
        /// <returns>The TestDataConnection instance.</returns>
        public virtual TestDataConnection Create(string invariantProviderName, string connectionString, List<string> dataFolders)
        {
            Debug.Assert(!string.IsNullOrEmpty(invariantProviderName), "invariantProviderName");
            Debug.Assert(!string.IsNullOrEmpty(connectionString), "connectionString");

            TestDataConnection.WriteDiagnostics("Create {0}, {1}", invariantProviderName, connectionString);

            // Most, but not all, connections are actually database based,
            // here we look for special cases
            TestDataConnectionFactory factory;
            if (specializedProviders.TryGetValue(invariantProviderName, out factory))
            {
                Debug.Assert(factory != null, "factory");
                return factory.Create(invariantProviderName, connectionString, dataFolders);
            }
            else
            {
                // Default is to use a conventional SQL based connection, this create method in turn
                // handles variations between DB based implementations
                return TestDataConnectionSql.Create(invariantProviderName, connectionString, dataFolders);
            }
        }

        #region TestDataConnectionFactories

        private class XmlTestDataConnectionFactory : TestDataConnectionFactory
        {
            public override TestDataConnection Create(string invariantProviderName, string connectionString, List<string> dataFolders)
            {
                return new XmlDataConnection(connectionString, dataFolders);
            }
        }

        private class CsvTestDataConnectionFactory : TestDataConnectionFactory
        {
            public override TestDataConnection Create(string invariantProviderName, string connectionString, List<string> dataFolders)
            {
                return new CsvDataConnection(connectionString, dataFolders);
            }
        }

        #endregion TestDataConnectionFactories
    }
}
