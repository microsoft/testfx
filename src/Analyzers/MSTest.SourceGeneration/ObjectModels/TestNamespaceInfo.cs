// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.Testing.Framework.SourceGeneration.Helpers;

namespace Microsoft.Testing.Framework.SourceGeneration.ObjectModels;

internal sealed class TestNamespaceInfo : IEquatable<TestNamespaceInfo>
{
    private readonly string _nameOrGlobalNamespace;
    private readonly string _containingAssembly;

    public string Name { get; }

    public string FullyQualifiedName { get; }

    public bool IsGlobalNamespace { get; }

    public TestNamespaceInfo(INamespaceSymbol namespaceSymbol)
    {
        _containingAssembly = namespaceSymbol.ContainingAssembly.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        Name = namespaceSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        _nameOrGlobalNamespace = namespaceSymbol.ToDisplayString();
        IsGlobalNamespace = namespaceSymbol.IsGlobalNamespace;
        FullyQualifiedName = namespaceSymbol.IsGlobalNamespace
            ? string.Empty
            : namespaceSymbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
    }

    public void AppendNamespaceTestNode(IndentedStringBuilder nodeStringBuilder, string testsVariableName)
    {
        using (nodeStringBuilder.AppendTestNode(_containingAssembly + "." + _nameOrGlobalNamespace, _nameOrGlobalNamespace, [], testsVariableName))
        {
        }
    }

    public bool Equals(TestNamespaceInfo? other)
        => other is not null
        && other.FullyQualifiedName == FullyQualifiedName;

    public override bool Equals(object? obj)
        => Equals(obj as TestNamespaceInfo);

    public override int GetHashCode()
        => FullyQualifiedName.GetHashCode();
}
