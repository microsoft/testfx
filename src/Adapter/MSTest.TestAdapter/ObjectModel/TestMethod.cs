// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;

using Microsoft.TestPlatform.AdapterUtilities;
using Microsoft.TestPlatform.AdapterUtilities.ManagedNameUtilities;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ObjectModel;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

/// <summary>
/// TestMethod contains information about a unit test method that needs to be executed.
/// </summary>
[Serializable]
public sealed class TestMethod : ITestMethod
{
    /// <summary>
    /// Number of elements in <see cref="Hierarchy"/>.
    /// </summary>
    public const int TotalHierarchyLevels = HierarchyConstants.Levels.TotalLevelCount;

    #region Fields
    private readonly IReadOnlyCollection<string> _hierarchy;
    private string _declaringClassFullName = null;
    private string _declaringAssemblyName = null;
    #endregion

    public TestMethod(string name, string fullClassName, string assemblyName, bool isAsync)
    {
        if (string.IsNullOrEmpty(assemblyName))
        {
            throw new ArgumentNullException(nameof(assemblyName));
        }

        Debug.Assert(!string.IsNullOrEmpty(name), "TestName cannot be empty");
        Debug.Assert(!string.IsNullOrEmpty(fullClassName), "Full className cannot be empty");

        Name = name;
        FullClassName = fullClassName;
        AssemblyName = assemblyName;
        IsAsync = isAsync;

        var hierarchy = new string[HierarchyConstants.Levels.TotalLevelCount];
        hierarchy[HierarchyConstants.Levels.ContainerIndex] = null;
        hierarchy[HierarchyConstants.Levels.NamespaceIndex] = fullClassName;
        hierarchy[HierarchyConstants.Levels.ClassIndex] = name;
        hierarchy[HierarchyConstants.Levels.TestGroupIndex] = name;

        _hierarchy = new ReadOnlyCollection<string>(hierarchy);
    }

    internal TestMethod(MethodBase method, string name, string fullClassName, string assemblyName, bool isAsync)
        : this(name, fullClassName, assemblyName, isAsync)
    {
        if (method == null)
        {
            throw new ArgumentNullException(nameof(method));
        }

        ManagedNameHelper.GetManagedName(method, out var managedType, out var managedMethod, out var hierarchyValues);
        hierarchyValues[HierarchyConstants.Levels.ContainerIndex] = null; // This one will be set by test windows to current test project name.

        ManagedTypeName = managedType;
        ManagedMethodName = managedMethod;
        _hierarchy = new ReadOnlyCollection<string>(hierarchyValues);
    }

    internal TestMethod(string managedTypeName, string managedMethodName, string[] hierarchyValues, string name, string fullClassName, string assemblyName, bool isAsync)
        : this(name, fullClassName, assemblyName, isAsync)
    {
        ManagedTypeName = managedTypeName;
        ManagedMethodName = managedMethodName;
        _hierarchy = new ReadOnlyCollection<string>(hierarchyValues);
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
    public string DeclaringAssemblyName
    {
        get
        {
            return _declaringAssemblyName;
        }

        set
        {
            Debug.Assert(value != AssemblyName, "DeclaringAssemblyName should not be the same as AssemblyName.");
            _declaringAssemblyName = value;
        }
    }

    /// <summary>
    /// Gets or sets the declaring class full name.
    /// This will be used to resolve overloads and while getting navigation data.
    /// This will be null if FullClassName is same as DeclaringClassFullName.
    /// Reason to set to null in the above case is to minimize the transfer of data across appdomains and not have a perf hit.
    /// </summary>
    public string DeclaringClassFullName
    {
        get
        {
            return _declaringClassFullName;
        }

        set
        {
            Debug.Assert(value != FullClassName, "DeclaringClassFullName should not be the same as FullClassName.");
            _declaringClassFullName = value;
        }
    }

    /// <inheritdoc />
    public string AssemblyName { get; private set; }

    /// <inheritdoc />
    public bool IsAsync { get; private set; }

    /// <inheritdoc />
    public string ManagedTypeName { get; }

    /// <inheritdoc />
    public string ManagedMethodName { get; }

    /// <inheritdoc />
    public bool HasManagedMethodAndTypeProperties => !string.IsNullOrWhiteSpace(ManagedTypeName) && !string.IsNullOrWhiteSpace(ManagedMethodName);

    /// <inheritdoc />
    public IReadOnlyCollection<string> Hierarchy => _hierarchy;

    /// <summary>
    /// Gets or sets type of dynamic data if any.
    /// </summary>
    internal DynamicDataType DataType { get; set; }

    /// <summary>
    /// Gets or sets the serialized data.
    /// </summary>
    internal string[] SerializedData { get; set; }

    /// <summary>
    /// Gets or sets the test group set during discovery.
    /// </summary>
    internal string TestGroup { get; set; }

    /// <summary>
    /// Gets or sets the display name set during discovery.
    /// </summary>
    internal string DisplayName { get; set; }

    internal TestMethod Clone() => MemberwiseClone() as TestMethod;
}
