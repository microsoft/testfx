// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using System.Collections.Immutable;

using Analyzers.Utilities;

using Microsoft.CodeAnalysis;
using Microsoft.Testing.Framework.SourceGeneration.Helpers;

using MSTest.SourceGeneration.Helpers;

namespace Microsoft.Testing.Framework.SourceGeneration.ObjectModels;

internal sealed record class TestTypeInfo
{
    private readonly string _name;
    private readonly string _containingAssemblyName;
    private readonly EquatableArray<(string FilePath, int StartLine, int EndLine)> _declarationReferences;

    internal EquatableArray<TestMethodInfo> TestMethodNodes { get; }

    public TimeSpan? TestExecutionTimeout { get; }

    internal string GeneratedTypeName { get; }

    internal string FullyQualifiedName { get; }

    internal TestNamespaceInfo ContainingNamespace { get; }

    internal bool IsIAsyncDisposable { get; }

    internal bool IsIDisposable { get; }

    internal string ConstructorShortName { get; }

    private TestTypeInfo(INamedTypeSymbol namedTypeSymbol, IMethodSymbol ctorToUse,
        WellKnownTypes wellKnownTypes, ImmutableArray<TestMethodInfo> testMethodNodes, TimeSpan? testExecutionTimeout)
    {
        _name = namedTypeSymbol.Name;
        _declarationReferences = namedTypeSymbol.DeclaringSyntaxReferences
            .Select(x => (x.SyntaxTree.FilePath, x.SyntaxTree.GetLineSpan(x.Span)))
            .Select(tuple => (tuple.FilePath, tuple.Item2.StartLinePosition.Line + 1, tuple.Item2.EndLinePosition.Line + 1))
            .ToImmutableArray();
        TestMethodNodes = testMethodNodes;
        TestExecutionTimeout = testExecutionTimeout;
        _containingAssemblyName = namedTypeSymbol.ContainingAssembly.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        ContainingNamespace = new(namedTypeSymbol.ContainingNamespace);
        FullyQualifiedName = namedTypeSymbol.ToDisplayString();
        string escapedFullyQualifiedName = TestNodeHelpers.GenerateEscapedName(FullyQualifiedName);
        GeneratedTypeName = ContainingNamespace.IsGlobalNamespace
            ? "_" + escapedFullyQualifiedName
            : escapedFullyQualifiedName;

        // 'SymbolDisplayFormat.CSharpShortErrorMessageFormat' gives us the minimal name while preserving sub-classes
        ConstructorShortName = ctorToUse.ContainingType.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat);

        IsIAsyncDisposable = namedTypeSymbol.AllInterfaces.Any(i =>
            SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, wellKnownTypes.IAsyncDisposableSymbol));
        IsIDisposable = namedTypeSymbol.AllInterfaces.Any(i =>
            SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, wellKnownTypes.IDisposableSymbol));
    }

    public static TestTypeInfo? TryBuild(GeneratorAttributeSyntaxContext context, WellKnownTypes wellKnownTypes)
    {
        if (context.TargetSymbol is not INamedTypeSymbol namedTypeSymbol)
        {
            return null;
        }

        // The generator syntax checks should have already filtered out any types that are not public/internal but we still need
        // to check because a public subclass of a non-public class is still not public.
        if (namedTypeSymbol.GetResultantVisibility() is not SymbolVisibility.Public and not SymbolVisibility.Internal)
        {
            return null;
        }

        // We only support simple classes
        if (namedTypeSymbol.IsAbstract
            || namedTypeSymbol.IsAnonymousType
            || namedTypeSymbol.IsGenericType
            || namedTypeSymbol.IsImplicitClass)
        {
            return null;
        }

        if (!HasParameterlessConstructor(namedTypeSymbol, out IMethodSymbol? parameterlessCtor))
        {
            return null;
        }

        TimeSpan? testExecutionTimeout = null;
        foreach (AttributeData attribute in namedTypeSymbol.GetAttributes())
        {
            if (attribute.TryGetTestExecutionTimeout(wellKnownTypes.TestExecutionTimeoutAttributeSymbol, wellKnownTypes.TimeSpanSymbol,
                out TimeSpan maybeExecutionTimeout))
            {
                testExecutionTimeout = maybeExecutionTimeout;
            }
        }

        var testMethodNodes = namedTypeSymbol
            .GetAllMembers()
            .SelectMany(members => members)
            .OfType<IMethodSymbol>()
            .Select(method => TestMethodInfo.TryBuild(method, namedTypeSymbol, wellKnownTypes))
            .WhereNotNull()
            .ToImmutableArray();

        return new(namedTypeSymbol, parameterlessCtor, wellKnownTypes, testMethodNodes, testExecutionTimeout);
    }

    private static bool HasParameterlessConstructor(INamedTypeSymbol namedTypeSymbol, [NotNullWhen(true)] out IMethodSymbol? parameterlessConstructor)
    {
        parameterlessConstructor = namedTypeSymbol.InstanceConstructors
            .FirstOrDefault(ctor => ctor.DeclaredAccessibility == Accessibility.Public && ctor.Parameters.Length == 0);

        return parameterlessConstructor != null;
    }

    public void AppendTestNode(IndentedStringBuilder sourceStringBuilder)
    {
        IDisposable? block = null;

        try
        {
            if (!ContainingNamespace.IsGlobalNamespace)
            {
                sourceStringBuilder.Append("namespace ");
                // TODO: Understand how to retrieve assembly default namespace and use it instead of assembly name
                block = sourceStringBuilder.AppendBlock(ContainingNamespace.FullyQualifiedName);
            }

            sourceStringBuilder.AppendLine("using Threading = global::System.Threading;");
            sourceStringBuilder.AppendLine("using ColGen = global::System.Collections.Generic;");
            sourceStringBuilder.AppendLine("using CA = global::System.Diagnostics.CodeAnalysis;");
            sourceStringBuilder.AppendLine("using Sys = global::System;");

            sourceStringBuilder.AppendLine("using Msg = global::Microsoft.Testing.Platform.Extensions.Messages;");
            sourceStringBuilder.AppendLine("using MSTF = global::Microsoft.Testing.Framework;");

            sourceStringBuilder.AppendLine();

            sourceStringBuilder.AppendLine("[CA::ExcludeFromCodeCoverage]");
            using (sourceStringBuilder.AppendBlock($"public static class {GeneratedTypeName}"))
            {
                sourceStringBuilder.Append("public static readonly MSTF::TestNode TestNode = ");
                AppendTestNodeCreation(sourceStringBuilder);
            }
        }
        finally
        {
            block?.Dispose();
        }
    }

    private void AppendTestNodeCreation(IndentedStringBuilder sourceStringBuilder)
    {
        List<string> properties = [];
        foreach ((string filePath, int startLine, int endLine) in _declarationReferences)
        {
            properties.Add($"new Msg::TestFileLocationProperty(@\"{filePath}\", new(new({startLine}, -1), new({endLine}, -1))),");
        }

        using (sourceStringBuilder.AppendTestNode(_containingAssemblyName + "." + FullyQualifiedName, _name, properties, ';'))
        {
            foreach (TestMethodInfo testMethod in TestMethodNodes)
            {
                testMethod.AppendTestNode(sourceStringBuilder, this);
            }
        }
    }
}
