// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.TestPlatform.AdapterUtilities;
using Microsoft.TestPlatform.AdapterUtilities.ManagedNameUtilities;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Services;

/// <summary>
/// Implementation of <see cref="IManagedNameUtilityService"/> that wraps Microsoft.TestPlatform.AdapterUtilities.
/// </summary>
internal sealed class ManagedNameUtilityService : IManagedNameUtilityService
{
    /// <summary>
    /// Gets the singleton instance of the <see cref="ManagedNameUtilityService"/>.
    /// </summary>
    public static ManagedNameUtilityService Instance { get; } = new();

    /// <inheritdoc />
    public string HierarchyLabel => HierarchyConstants.HierarchyLabel;

    /// <inheritdoc />
    public string HierarchyPropertyId => HierarchyConstants.HierarchyPropertyId;

    /// <inheritdoc />
    public int TotalHierarchyLevelCount => HierarchyConstants.Levels.TotalLevelCount;

    /// <inheritdoc />
    public int HierarchyContainerIndex => HierarchyConstants.Levels.ContainerIndex;

    /// <inheritdoc />
    public int HierarchyNamespaceIndex => HierarchyConstants.Levels.NamespaceIndex;

    /// <inheritdoc />
    public int HierarchyClassIndex => HierarchyConstants.Levels.ClassIndex;

    /// <inheritdoc />
    public int HierarchyTestGroupIndex => HierarchyConstants.Levels.TestGroupIndex;

    /// <inheritdoc />
    public string ManagedTypeLabel => ManagedNameConstants.ManagedTypeLabel;

    /// <inheritdoc />
    public string ManagedTypePropertyId => ManagedNameConstants.ManagedTypePropertyId;

    /// <inheritdoc />
    public string ManagedMethodLabel => ManagedNameConstants.ManagedMethodLabel;

    /// <inheritdoc />
    public string ManagedMethodPropertyId => ManagedNameConstants.ManagedMethodPropertyId;

    /// <inheritdoc />
    public void GetManagedName(MethodBase method, out string managedTypeName, out string managedMethodName, out string?[]? hierarchyValues)
        => ManagedNameHelper.GetManagedName(method, out managedTypeName, out managedMethodName, out hierarchyValues);

    /// <inheritdoc />
    public MethodBase? GetMethod(Assembly assembly, string managedTypeName, string managedMethodName)
    {
        try
        {
            return ManagedNameHelper.GetMethod(assembly, managedTypeName, managedMethodName);
        }
        catch (InvalidManagedNameException)
        {
            return null;
        }
    }
}
