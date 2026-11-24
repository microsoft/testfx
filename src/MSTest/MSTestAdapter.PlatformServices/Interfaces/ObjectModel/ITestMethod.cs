// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable CA1716 // Do not use reserved keywords
namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ObjectModel;
#pragma warning restore CA1716 // Do not use reserved keywords

/// <summary>
/// TestMethod structure that is shared between adapter and platform services only.
/// </summary>
internal interface ITestMethod
{
    /// <summary>
    /// Gets the name of the test method.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the semantic full class name of the test method.
    /// </summary>
    string FullClassName { get; }

    /// <summary>
    /// Gets the name of the test assembly.
    /// </summary>
    string AssemblyName { get; }

    /// <summary>
    /// Gets the fully specified type name metadata format.
    /// </summary>
    /// <example>
    ///     <c>NamespaceA.NamespaceB.ClassName`1+InnerClass`2</c>.
    /// </example>
    /// <remarks>
    /// This value is derived from <see cref="FullClassName"/>. Closed generic type arguments are omitted because
    /// managed type metadata uses the open generic type definition.
    /// </remarks>
    string? ManagedTypeName { get; }

    /// <summary>
    /// Gets the fully specified method name metadata format.
    /// </summary>
    /// <example>
    ///     <c>MethodName`2(ParamTypeA,ParamTypeB,...)</c>.
    /// </example>
    string? ManagedMethodName { get; }

    /// <summary>
    /// Gets a value indicating whether the managed method metadata is available.
    /// <see cref="ManagedTypeName"/> is derived from <see cref="FullClassName"/>.
    /// </summary>
    bool HasManagedMethodAndTypeProperties { get; }

    /// <summary>
    /// Gets the test case hierarchy parsed by the adapter.
    /// </summary>
    /// <remarks>
    /// Contains four items in order: Namespace, class name, test group, display name.
    /// </remarks>
    IReadOnlyCollection<string?>? Hierarchy { get; }
}
