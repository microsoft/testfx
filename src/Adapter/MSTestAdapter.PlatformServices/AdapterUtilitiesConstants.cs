// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

/// <summary>
/// Constants for managed name properties used in test case identification.
/// These constants mirror the values from Microsoft.TestPlatform.AdapterUtilities.
/// </summary>
internal static class ManagedNameConstants
{
    /// <summary>
    /// Label for managed type property.
    /// </summary>
    public const string ManagedTypeLabel = "ManagedType";

    /// <summary>
    /// Property id for managed type property.
    /// </summary>
    public const string ManagedTypePropertyId = "TestCase.ManagedType";

    /// <summary>
    /// Label for managed method property.
    /// </summary>
    public const string ManagedMethodLabel = "ManagedMethod";

    /// <summary>
    /// Property id for managed method property.
    /// </summary>
    public const string ManagedMethodPropertyId = "TestCase.ManagedMethod";
}

/// <summary>
/// Constants for test hierarchy properties.
/// These constants mirror the values from Microsoft.TestPlatform.AdapterUtilities.
/// </summary>
internal static class HierarchyConstants
{
    /// <summary>
    /// Label for hierarchy property.
    /// </summary>
    public const string HierarchyLabel = "Hierarchy";

    /// <summary>
    /// Property id for hierarchy property.
    /// </summary>
    public const string HierarchyPropertyId = "TestCase.Hierarchy";

    /// <summary>
    /// Constants for hierarchy levels.
    /// </summary>
    internal static class Levels
    {
        /// <summary>
        /// Total number of levels in the hierarchy.
        /// </summary>
        public const int TotalLevelCount = 4;

        /// <summary>
        /// Index of the container element in the hierarchy array.
        /// </summary>
        public const int ContainerIndex = 0;

        /// <summary>
        /// Index of the namespace element in the hierarchy array.
        /// </summary>
        public const int NamespaceIndex = 1;

        /// <summary>
        /// Index of the class element in the hierarchy array.
        /// </summary>
        public const int ClassIndex = 2;

        /// <summary>
        /// Index of the test group element in the hierarchy array.
        /// </summary>
        public const int TestGroupIndex = 3;
    }
}
