// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

using Microsoft.TestPlatform.AdapterUtilities;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using ITestMethod = Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ObjectModel.ITestMethod;
using TestIdGenerationStrategy = Microsoft.VisualStudio.TestTools.UnitTesting.TestIdGenerationStrategy;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

/// <summary>
/// TestMethod contains information about a unit test method that needs to be executed.
/// </summary>
#if NET6_0_OR_GREATER
[Obsolete(Constants.PublicTypeObsoleteMessage, DiagnosticId = "MSTESTOBS")]
#else
[Obsolete(Constants.PublicTypeObsoleteMessage)]
#endif
[Serializable]
public sealed class TestMethod : ITestMethod
{
    /// <summary>
    /// Number of elements in <see cref="Hierarchy"/>.
    /// </summary>
    public const int TotalHierarchyLevels = HierarchyConstants.Levels.TotalLevelCount;

    private readonly ReadOnlyCollection<string?> _hierarchy;
    private string? _declaringClassFullName;
    private string? _declaringAssemblyName;

    public TestMethod(string name, string fullClassName, string assemblyName, bool isAsync)
        : this(null, null, null, name, fullClassName, assemblyName, isAsync, null, TestIdGenerationStrategy.FullyQualified)
    {
    }

    internal TestMethod(string name, string fullClassName, string assemblyName, bool isAsync, string? displayName,
        TestIdGenerationStrategy testIdGenerationStrategy)
        : this(null, null, null, name, fullClassName, assemblyName, isAsync, displayName, testIdGenerationStrategy)
    {
    }

    internal TestMethod(string? managedTypeName, string? managedMethodName, string?[]? hierarchyValues, string name,
        string fullClassName, string assemblyName, bool isAsync, string? displayName,
        TestIdGenerationStrategy testIdGenerationStrategy)
    {
        Guard.NotNullOrWhiteSpace(assemblyName);

        Name = name;
        DisplayName = displayName ?? name;
        FullClassName = fullClassName;
        AssemblyName = assemblyName;
        IsAsync = isAsync;

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
        TestIdGenerationStrategy = testIdGenerationStrategy;
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public string FullClassName { get; }

    /// <summary>
    /// Gets or sets the declaring assembly full name. This will be used while getting navigation data.
    /// This will be null if AssemblyName is same as DeclaringAssemblyName.
    /// Reason to set to null in the above case is to minimize the transfer of data across appdomains and not have a performance hit.
    /// </summary>
    public string? DeclaringAssemblyName
    {
        get => _declaringAssemblyName;

        set
        {
            DebugEx.Assert(value != AssemblyName, "DeclaringAssemblyName should not be the same as AssemblyName.");
            _declaringAssemblyName = value;
        }
    }

    /// <summary>
    /// Gets or sets the declaring class full name.
    /// This will be used to resolve overloads and while getting navigation data.
    /// This will be null if FullClassName is same as DeclaringClassFullName.
    /// Reason to set to null in the above case is to minimize the transfer of data across appdomains and not have a perf hit.
    /// </summary>
    public string? DeclaringClassFullName
    {
        get => _declaringClassFullName;

        set
        {
            DebugEx.Assert(value != FullClassName, "DeclaringClassFullName should not be the same as FullClassName.");
            _declaringClassFullName = value;
        }
    }

    /// <inheritdoc />
    public string AssemblyName { get; private set; }

    /// <inheritdoc />
    public bool IsAsync { get; private set; }

    /// <inheritdoc />
    public string? ManagedTypeName { get; }

    /// <inheritdoc />
    public string? ManagedMethodName { get; }

    /// <inheritdoc />
    [MemberNotNullWhen(true, nameof(ManagedTypeName), nameof(ManagedMethodName))]
    public bool HasManagedMethodAndTypeProperties => !StringEx.IsNullOrWhiteSpace(ManagedTypeName) && !StringEx.IsNullOrWhiteSpace(ManagedMethodName);

    /// <inheritdoc />
    public IReadOnlyCollection<string?> Hierarchy => _hierarchy;

    /// <inheritdoc />
    public TestIdGenerationStrategy TestIdGenerationStrategy { get; }

    /// <summary>
    /// Gets or sets type of dynamic data if any.
    /// </summary>
    internal DynamicDataType DataType { get; set; }

    /// <summary>
    /// Gets or sets the serialized data.
    /// </summary>
    internal string?[]? SerializedData { get; set; }

    /// <summary>
    /// Gets or sets the test group set during discovery.
    /// </summary>
    internal string? TestGroup { get; set; }

    /// <summary>
    /// Gets the display name set during discovery.
    /// </summary>
    internal string DisplayName { get; }

    internal string UniqueName
        => HasManagedMethodAndTypeProperties
        ? $"{ManagedTypeName}.{ManagedMethodName}->{string.Join(", ", SerializedData ?? [])}"
        : $"{FullClassName}.{Name}->{string.Join(", ", SerializedData ?? [])}";

    internal TestMethod Clone() => (TestMethod)MemberwiseClone();
}
