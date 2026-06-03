// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Analyzers;

/// <summary>
/// MSTEST0069: warns when a class is intended as an MSTest test class via an inherited
/// <c>[TestClass]</c> attribute. The MSTest source generator only enumerates types that
/// declare <c>[TestClass]</c> directly on themselves (it uses
/// <c>SyntaxValueProvider.ForAttributeWithMetadataName</c> which does not follow inheritance),
/// so any test class that relies on an inherited <c>[TestClass]</c> will be silently skipped
/// from source-generated discovery and will not be runnable when the source-generated
/// provider is the active reflection backend.
/// </summary>
/// <remarks>
/// This analyzer ships in the MSTest.SourceGeneration package, so it is only loaded for
/// projects that have opted into source generation. The diagnostic ID <c>MSTEST0069</c> is
/// reserved in <c>MSTest.Analyzers.Helpers.DiagnosticIds</c> to avoid collisions with the
/// regular MSTest analyzers package.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InheritedTestClassAttributeWithSourceGeneratorAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic id reported by this analyzer.</summary>
    public const string DiagnosticId = "MSTEST0069";

    private const string TestClassAttributeMetadataName = "Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Inherited [TestClass] is ignored by the MSTest source generator",
        messageFormat: "Class '{0}' inherits the [TestClass] attribute from base class '{1}'. The MSTest source generator only discovers types that declare [TestClass] directly; apply [TestClass] to '{0}' so it is discovered when source-generated discovery is active.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The MSTest source generator uses ForAttributeWithMetadataName to enumerate test classes, which does not follow inheritance. A class that relies on inheriting [TestClass] from a base type will be silently skipped from discovery in source-generation mode. Apply [TestClass] directly to the derived class to keep it discoverable.",
        helpLinkUri: "https://aka.ms/mstest/analyzers");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            INamedTypeSymbol? testClassAttribute = compilationContext.Compilation.GetTypeByMetadataName(TestClassAttributeMetadataName);
            if (testClassAttribute is null)
            {
                return;
            }

            compilationContext.RegisterSymbolAction(
                symbolContext => AnalyzeNamedType(symbolContext, testClassAttribute),
                SymbolKind.NamedType);
        });
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context, INamedTypeSymbol testClassAttribute)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        // Source generator skips static, abstract, open-generic, and file-local classes — they
        // cannot be test classes regardless of the [TestClass] inheritance path.
        if (type.TypeKind != TypeKind.Class || type.IsStatic || type.IsAbstract || type.IsImplicitClass)
        {
            return;
        }

        if (HasDirectAttribute(type, testClassAttribute))
        {
            return;
        }

        INamedTypeSymbol? baseWithAttribute = FindBaseTypeWithAttribute(type.BaseType, testClassAttribute);
        if (baseWithAttribute is null)
        {
            return;
        }

        Location location = type.Locations.Length > 0 ? type.Locations[0] : Location.None;
        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            location,
            type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            baseWithAttribute.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
    }

    private static bool HasDirectAttribute(ISymbol symbol, INamedTypeSymbol attributeType)
    {
        foreach (AttributeData attribute in symbol.GetAttributes())
        {
            if (IsOrInheritsFrom(attribute.AttributeClass, attributeType))
            {
                return true;
            }
        }

        return false;
    }

    private static INamedTypeSymbol? FindBaseTypeWithAttribute(INamedTypeSymbol? baseType, INamedTypeSymbol attributeType)
    {
        for (INamedTypeSymbol? current = baseType; current is not null; current = current.BaseType)
        {
            if (HasDirectAttribute(current, attributeType))
            {
                return current;
            }
        }

        return null;
    }

    private static bool IsOrInheritsFrom(INamedTypeSymbol? candidate, INamedTypeSymbol target)
    {
        for (INamedTypeSymbol? current = candidate; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, target))
            {
                return true;
            }
        }

        return false;
    }
}
