// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using ITestMethod = Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ObjectModel.ITestMethod;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

/// <summary>
/// TestMethod contains information about a unit test method that needs to be executed.
/// </summary>
#if NETFRAMEWORK
[Serializable]
#endif
internal sealed class TestMethod : ITestMethod
{
    /// <summary>
    /// Number of elements in <see cref="Hierarchy"/>.
    /// </summary>
    public const int TotalHierarchyLevels = HierarchyConstants.Levels.TotalLevelCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestMethod"/> class.
    /// </summary>
    /// <param name="name">The name of the method.</param>
    /// <param name="fullClassName">The full name of the class containing the method in execution context.</param>
    /// <param name="assemblyName">The full assembly name.</param>
    /// <param name="displayName">The display name of the test method.</param>
    // This constructor is used in testing only, and we should remove it in the future and update the tests.
    internal TestMethod(string name, string fullClassName, string assemblyName, string? displayName)
        : this(null, null, name, fullClassName, assemblyName, displayName, null)
    {
    }

    internal TestMethod(
        string? managedMethodName,
        string?[]? hierarchyValues,
        string name,
        string fullClassName,
        string assemblyName,
        string? displayName,
        string? parameterTypes)
    {
        Ensure.NotNullOrWhiteSpace(assemblyName);

        Name = name;
        DisplayName = displayName ?? name;
        FullClassName = fullClassName;
        ParameterTypes = parameterTypes;

        AssemblyName = assemblyName;

        if (hierarchyValues is not null)
        {
            Hierarchy = new ReadOnlyCollection<string?>(hierarchyValues);
        }

        ManagedMethodName = managedMethodName;
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public string FullClassName { get; }

    public string? ParameterTypes { get; }

    /// <inheritdoc />
    public string AssemblyName { get; private set; }

    /// <inheritdoc />
    public string? ManagedTypeName => GetManagedTypeName(FullClassName);

    /// <inheritdoc />
    public string? ManagedMethodName { get; }

    /// <inheritdoc />
    [MemberNotNullWhen(true, nameof(ManagedMethodName))]
    // ManagedTypeName is derived from FullClassName, so this only gates the managed method metadata.
    public bool HasManagedMethodAndTypeProperties => !StringEx.IsNullOrWhiteSpace(ManagedMethodName);

    /// <inheritdoc />
    public IReadOnlyCollection<string?>? Hierarchy { get; }

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
#if NETFRAMEWORK
    [field: NonSerialized]
#endif
    internal object?[]? ActualData { get; set; }

#if NETFRAMEWORK
    [field: NonSerialized]
#endif
    internal MethodInfo? MethodInfo { get; set; }

    private static string GetManagedTypeName(string fullClassName)
    {
        int genericArgumentsStart = fullClassName.IndexOf('[');
        return genericArgumentsStart >= 0
            ? fullClassName[..genericArgumentsStart]
            : fullClassName;
    }

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

    // NOTE: This method has a latent bug (it assigns AssemblyName/MethodInfo on `this` instead of on the
    // returned clone), tracked by https://github.com/microsoft/testfx/issues/9573. It is intentionally left
    // untouched here so the existing ToTestCase / test-case filter bridge callers keep their exact current
    // behavior; the execution engine uses the correct CloneWithSource below instead. The two are unified once
    // #9573 is addressed.
    internal TestMethod CloneWithUpdatedSource(string source)
    {
        var clone = (TestMethod)MemberwiseClone();
        clone.AssemblyName = source;
        clone.MethodInfo = null;
        return clone;
    }

    // Correct counterpart of CloneWithUpdatedSource: assigns the updated source and clears the cached
    // MethodInfo on the returned clone (leaving `this` untouched). Used by the execution engine's source
    // resolution. See https://github.com/microsoft/testfx/issues/9573.
    internal TestMethod CloneWithSource(string source)
    {
        var clone = (TestMethod)MemberwiseClone();
        clone.AssemblyName = source;
        clone.MethodInfo = null;
        return clone;
    }

    // Correct counterpart of CloneWithUpdatedSource: assigns the updated source and clears the cached
    // MethodInfo on the returned clone (leaving `this` untouched). Used by the execution engine's source
    // resolution. See https://github.com/microsoft/testfx/issues/9573.
    internal TestMethod CloneWithSource(string source)
    {
        var clone = (TestMethod)MemberwiseClone();
        clone.AssemblyName = source;
        clone.MethodInfo = null;
        return clone;
    }
}
