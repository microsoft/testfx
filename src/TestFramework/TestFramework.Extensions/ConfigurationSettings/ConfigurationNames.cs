// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Contains literals for names of sections, properties, attributes.
/// </summary>
internal static class ConfigurationNames
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    internal const string SectionName = "microsoft.visualstudio.testtools";

    /// <summary>
    /// The configuration section name for Beta2. Left around for compat.
    /// </summary>
    internal const string Beta2SectionName = "microsoft.visualstudio.qualitytools";

    /// <summary>
    /// Section name for Data source.
    /// </summary>
    internal const string DataSourcesSectionName = "dataSources";

    /// <summary>
    /// Attribute name for 'Name'.
    /// </summary>
    internal const string NameAttributeName = "name";

    /// <summary>
    /// Attribute name for 'ConnectionString'.
    /// </summary>
    internal const string ConnectionStringAttributeName = "connectionString";

    /// <summary>
    /// Attribute name for 'DataAccessMethod'.
    /// </summary>
    internal const string DataAccessMethodAttributeName = "dataAccessMethod";

    /// <summary>
    /// Attribute name for 'DataTable'.
    /// </summary>
    internal const string DataTableAttributeName = "dataTableName";

    /// <summary>
    /// Attribute name for 'LaunchDebuggerOnFailure'.
    /// </summary>
    internal const string LaunchDebuggerOnFailureAttributeName = "launchDebuggerOnFailure";

    /// <summary>
    /// Attribute name for 'DebuggerLaunchTestFilter'.
    /// </summary>
    internal const string DebuggerLaunchTestFilterAttributeName = "debuggerLaunchTestFilter";
}
#endif
