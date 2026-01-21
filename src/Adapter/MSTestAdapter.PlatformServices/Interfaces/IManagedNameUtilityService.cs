// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

/// <summary>
/// Service for managed name utilities and hierarchy constants.
/// Abstracts the dependency on Microsoft.TestPlatform.AdapterUtilities.
/// </summary>
internal interface IManagedNameUtilityService
{
    /// <summary>
    /// Gets the label to use on Hierarchy test property.
    /// </summary>
    string HierarchyLabel { get; }

    /// <summary>
    /// Gets the property id to use on Hierarchy test property.
    /// </summary>
    string HierarchyPropertyId { get; }

    /// <summary>
    /// Gets the total length of Hierarchy array.
    /// </summary>
    int TotalHierarchyLevelCount { get; }

    /// <summary>
    /// Gets the index of the test container element of the hierarchy array.
    /// </summary>
    int HierarchyContainerIndex { get; }

    /// <summary>
    /// Gets the index of the namespace element of the hierarchy array.
    /// </summary>
    int HierarchyNamespaceIndex { get; }

    /// <summary>
    /// Gets the index of the class element of the hierarchy array.
    /// </summary>
    int HierarchyClassIndex { get; }

    /// <summary>
    /// Gets the index of the test group element of the hierarchy array.
    /// </summary>
    int HierarchyTestGroupIndex { get; }

    /// <summary>
    /// Gets the label to use on ManagedType test property.
    /// </summary>
    string ManagedTypeLabel { get; }

    /// <summary>
    /// Gets the property id to use on ManagedType test property.
    /// </summary>
    string ManagedTypePropertyId { get; }

    /// <summary>
    /// Gets the label to use on ManagedMethod test property.
    /// </summary>
    string ManagedMethodLabel { get; }

    /// <summary>
    /// Gets the property id to use on ManagedMethod test property.
    /// </summary>
    string ManagedMethodPropertyId { get; }

    /// <summary>
    /// Gets the managed type name, managed method name, and hierarchy values for a test method.
    /// </summary>
    /// <param name="method">The method to get the managed name for.</param>
    /// <param name="managedTypeName">The managed type name.</param>
    /// <param name="managedMethodName">The managed method name.</param>
    /// <param name="hierarchyValues">The hierarchy values array.</param>
    void GetManagedName(MethodBase method, out string managedTypeName, out string managedMethodName, out string?[]? hierarchyValues);

    /// <summary>
    /// Gets the method from an assembly using the managed type name and managed method name.
    /// </summary>
    /// <param name="assembly">The assembly to search in.</param>
    /// <param name="managedTypeName">The managed type name.</param>
    /// <param name="managedMethodName">The managed method name.</param>
    /// <returns>The method base if found, null otherwise.</returns>
    MethodBase? GetMethod(Assembly assembly, string managedTypeName, string managedMethodName);
}
