﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;

using Microsoft.TestPlatform.AdapterUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using ITestMethod = Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ObjectModel.ITestMethod;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

/// <summary>
/// TestMethod contains information about a unit test method that needs to be executed.
/// </summary>
[Serializable]
internal sealed class TestMethod : ITestMethod
{
    /// <summary>
    /// Number of elements in <see cref="Hierarchy"/>.
    /// </summary>
    public const int TotalHierarchyLevels = HierarchyConstants.Levels.TotalLevelCount;

    private readonly ReadOnlyCollection<string?> _hierarchy;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestMethod"/> class.
    /// </summary>
    /// <param name="name">The name of the method.</param>
    /// <param name="fullClassName">The full name of the class declaring the method.</param>
    /// <param name="assemblyName">The full assembly name.</param>
    /// <param name="displayName">The display name of the test method.</param>
    internal TestMethod(string name, string fullClassName, string assemblyName, string? displayName)
        : this(null, null, null, name, fullClassName, assemblyName, displayName, null)
    {
    }

    internal TestMethod(string? managedTypeName, string? managedMethodName, string?[]? hierarchyValues, string name,
        string fullClassName, string assemblyName, string? displayName, string? parameterTypes)
    {
        Guard.NotNullOrWhiteSpace(assemblyName);

        Name = name;
        DisplayName = displayName ?? name;
        FullClassName = fullClassName;
        ParameterTypes = parameterTypes;

        AssemblyName = assemblyName;

        if (hierarchyValues is null)
        {
            hierarchyValues = new string?[HierarchyConstants.Levels.TotalLevelCount];
            hierarchyValues[HierarchyConstants.Levels.ContainerIndex] = null;
            hierarchyValues[HierarchyConstants.Levels.NamespaceIndex] = fullClassName;
            hierarchyValues[HierarchyConstants.Levels.ClassIndex] = name;
            hierarchyValues[HierarchyConstants.Levels.TestGroupIndex] = name;
        }

        _hierarchy = new ReadOnlyCollection<string?>(hierarchyValues);
        ManagedTypeName = managedTypeName;
        ManagedMethodName = managedMethodName;
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public string FullClassName { get; }

    public string? ParameterTypes { get; }

    /// <summary>
    /// Gets or sets the declaring class full name.
    /// This will be used to resolve overloads and while getting navigation data.
    /// This will be null if FullClassName is same as DeclaringClassFullName.
    /// Reason to set to null in the above case is to minimize the transfer of data across appdomains and not have a perf hit.
    /// </summary>
    public string? DeclaringClassFullName
    {
        get;

        set
        {
            DebugEx.Assert(value != FullClassName, "DeclaringClassFullName should not be the same as FullClassName.");
            field = value;
        }
    }

    /// <inheritdoc />
    public string AssemblyName { get; private set; }

    /// <inheritdoc />
    public string? ManagedTypeName { get; }

    /// <inheritdoc />
    public string? ManagedMethodName { get; }

    /// <inheritdoc />
    [MemberNotNullWhen(true, nameof(ManagedTypeName), nameof(ManagedMethodName))]
    public bool HasManagedMethodAndTypeProperties => !StringEx.IsNullOrWhiteSpace(ManagedTypeName) && !StringEx.IsNullOrWhiteSpace(ManagedMethodName);

    /// <inheritdoc />
    public IReadOnlyCollection<string?> Hierarchy => _hierarchy;

    /// <summary>
    /// Gets or sets type of dynamic data if any.
    /// </summary>
    internal DynamicDataType DataType { get; set; }

    /// <summary>
    /// Gets or sets the serialized data.
    /// </summary>
    internal string?[]? SerializedData { get; set; }

    internal int TestCaseIndex { get; set; }

    // This holds user types that may not be serializable.
    // If app domains are enabled, we have no choice other than losing the original data.
    // In that case, we fallback to deserializing the SerializedData.
    [field: NonSerialized]
    internal object?[]? ActualData { get; set; }

    [field: NonSerialized]
    internal MethodInfo? MethodInfo { get; set; }

    /// <summary>
    /// Gets or sets the test data source ignore message.
    /// </summary>
    /// <remarks>
    /// The test is ignored if this is set to non-null.
    /// </remarks>
    internal string? TestDataSourceIgnoreMessage { get; set; }

    /// <summary>
    /// Gets or sets the display name set during discovery.
    /// </summary>
    internal string DisplayName { get; set; }

    internal TestMethod Clone() => (TestMethod)MemberwiseClone();

    internal TestMethod CloneWithUpdatedSource(string source)
    {
        var clone = (TestMethod)MemberwiseClone();
        AssemblyName = source;
        MethodInfo = null;
        return clone;
    }
}
