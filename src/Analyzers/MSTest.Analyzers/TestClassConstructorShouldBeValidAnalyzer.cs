// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0063: <inheritdoc cref="Resources.TestClassConstructorShouldBeValidTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class TestClassConstructorShouldBeValidAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.TestClassConstructorShouldBeValidTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.TestClassConstructorShouldBeValidDescription), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.TestClassConstructorShouldBeValidMessageFormat), Resources.ResourceManager, typeof(Resources));

    /// <inheritdoc cref="Resources.TestClassConstructorShouldBeValidTitle" />
    public static readonly DiagnosticDescriptor TestClassConstructorShouldBeValidRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.TestClassConstructorShouldBeValidRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(TestClassConstructorShouldBeValidRule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestClassAttribute, out INamedTypeSymbol? testClassAttributeSymbol))
            {
                INamedTypeSymbol? testContextSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestContext);
                context.RegisterSymbolAction(
                    context => AnalyzeSymbol(context, testClassAttributeSymbol, testContextSymbol),
                    SymbolKind.NamedType);
            }
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol testClassAttributeSymbol, INamedTypeSymbol? testContextSymbol)
    {
        var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;
        if (namedTypeSymbol.TypeKind != TypeKind.Class
            || !namedTypeSymbol.GetAttributes().Any(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, testClassAttributeSymbol)))
        {
            return;
        }

        // Check if there's at least one valid constructor
        bool hasValidConstructor = false;
        bool hasAnyConstructor = false;

        foreach (IMethodSymbol constructor in namedTypeSymbol.Constructors)
        {
            // Skip implicit constructors
            if (constructor.IsImplicitlyDeclared)
            {
                continue;
            }

            hasAnyConstructor = true;

            // Check if constructor is public
            if (constructor.DeclaredAccessibility != Accessibility.Public)
            {
                continue;
            }

            // Check if parameterless
            if (constructor.Parameters.Length == 0)
            {
                hasValidConstructor = true;
                break;
            }

            // Check if single parameter of type TestContext
            if (constructor.Parameters.Length == 1
                && testContextSymbol is not null
                && SymbolEqualityComparer.Default.Equals(constructor.Parameters[0].Type, testContextSymbol))
            {
                hasValidConstructor = true;
                break;
            }
        }

        // If there are explicit constructors but none are valid, report diagnostic
        if (hasAnyConstructor && !hasValidConstructor)
        {
            context.ReportDiagnostic(namedTypeSymbol.CreateDiagnostic(TestClassConstructorShouldBeValidRule, namedTypeSymbol.Name));
        }
    }
}
