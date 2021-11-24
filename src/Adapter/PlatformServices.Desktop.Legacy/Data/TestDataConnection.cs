// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Data
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.Common;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Security;

    /// <summary>
    /// This used to be "DataUtility", a helper class to handle quoted strings etc for different
    /// data providers but the purpose has been expanded to be a general abstraction over a
    /// connection, including the ability to read data and metadata (tables and columns)
    /// </summary>
    internal abstract class TestDataConnection : IDisposable
    {
        internal const string ConnectionDirectoryKey = "|DataDirectory|\\";

        private static bool? extendedDiagnosticsEnabled;

        // List of places to look for files when substituting |DataDirectory|
        private List<string> dataFolders;

        internal protected TestDataConnection(List<string> dataFolders)
        {
            this.dataFolders = dataFolders;
        }

        /// <summary>
        /// Gets the connection.
        /// </summary>
        /// <remarks>This will only return non-null for true DB based connections (TestDataConnectionSql)</remarks>
        public virtual DbConnection Connection
        {
            get { return null; }
        }

        private static bool ExtendedDiagnosticsEnabled
        {
            get
            {
                if (!extendedDiagnosticsEnabled.HasValue)
                {
                    // We use an environment variable so that we can enable this extended
                    // diagnostic trace
                    try
                    {
                        string value = Environment.GetEnvironmentVariable("VSTS_DIAGNOSTICS");
                        extendedDiagnosticsEnabled = (value != null) && value.Contains("TestDataConnection");
                    }
                    catch (SecurityException)
                    {
                        extendedDiagnosticsEnabled = false;
                    }
                }

                return extendedDiagnosticsEnabled.Value;
            }
        }

        /// <summary>
        /// Get a list of tables and views for this connection. Filters out "system" tables
        /// </summary>
        /// <returns>List of names or null if error</returns>
        public abstract List<string> GetDataTablesAndViews();

        /// <summary>
        /// Given a table name, return a list of column names
        /// </summary>
        /// <param name="tableName">The name of the table.</param>
        /// <returns>List of names or null if error</returns>
        public abstract List<string> GetColumns(string tableName);

        /// <summary>
        /// Read the content of a table or view into memory
        /// Try to limit to columns specified, if columns is null, read all columns
        /// </summary>
        /// <param name="tableName">Minimally quoted table name</param>
        /// <param name="columns">Array of columns</param>
        /// <returns>Data table or null if error</returns>
        public abstract DataTable ReadTable(string tableName, IEnumerable columns);

        // It is critical that is class be disposed of properly, otherwise
        // data connections may be left open. In general it is best to use create instances
        // in a "using"
        public virtual void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        internal static bool PathNeedsFixup(string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                if (path.StartsWith(ConnectionDirectoryKey, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        // Only use this if "PathNeedsFixup" returns true
        internal static string GetRelativePart(string path)
        {
            Debug.Assert(PathNeedsFixup(path), "Incorrect path.");
            return path.Substring(ConnectionDirectoryKey.Length);
        }

        // Check a string to see if it has our magic prefix
        // and if it does, assume what follows is a relative
        // path, which we then convert by making it a full path
        // otherwise return null
        internal static string FixPath(string path, List<string> foldersToCheck)
        {
            if (PathNeedsFixup(path))
            {
                string relPath = GetRelativePart(path);

                // First bet, relative to the current directory
                string fullPath = Path.GetFullPath(relPath);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }

                // Second bet, any on our folders foldersToCheck list
                if (foldersToCheck != null)
                {
                    foreach (string folder in foldersToCheck)
                    {
                        fullPath = Path.GetFullPath(Path.Combine(folder, relPath));
                        if (File.Exists(fullPath))
                        {
                            return fullPath;
                        }
                    }
                }

                // Finally assume the file ended up directly in the current directory.
                return Path.GetFullPath(Path.GetFileName(relPath));
            }

            return null;
        }

        [Conditional("DEBUG")]
        protected internal static void WriteDiagnostics(string formatString, params object[] parameters)
        {
            if (ExtendedDiagnosticsEnabled)
            {
                Debug.WriteLine("TestDataConnection: " + string.Format(CultureInfo.InvariantCulture, formatString, parameters));
            }
        }

        protected string FixPath(string path)
        {
            return FixPath(path, this.dataFolders);
        }
    }
}
