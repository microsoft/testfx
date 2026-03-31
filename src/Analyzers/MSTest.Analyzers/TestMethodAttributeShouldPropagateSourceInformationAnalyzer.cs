// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0057: <inheritdoc cref="Resources.TestMethodAttributeShouldPropagateSourceInformationTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class TestMethodAttributeShouldPropagateSourceInformationAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.TestMethodAttributeShouldPropagateSourceInformationTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.TestMethodAttributeShouldPropagateSourceInformationMessageFormat), Resources.ResourceManager, typeof(Resources));

    /// <inheritdoc cref="Resources.TestMethodAttributeShouldPropagateSourceInformationTitle" />
    public static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.TestMethodAttributeShouldPropagateSourceInformationRuleId,
        Title,
        MessageFormat,
        null,
        Category.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute, out INamedTypeSymbol? testMethodAttributeSymbol)
                || !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeCompilerServicesCallerFilePathAttribute, out INamedTypeSymbol? callerFilePathAttributeSymbol)
                || !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeCompilerServicesCallerLineNumberAttribute, out INamedTypeSymbol? callerLineNumberAttributeSymbol))
            {
                return;
            }

            context.RegisterSymbolAction(
                context => AnalyzeSymbol(context, testMethodAttributeSymbol, callerFilePathAttributeSymbol, callerLineNumberAttributeSymbol),
                SymbolKind.NamedType);
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol testMethodAttributeSymbol, INamedTypeSymbol callerFilePathAttributeSymbol, INamedTypeSymbol callerLineNumberAttributeSymbol)
    {
        var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;

        // Only analyze classes that derive from TestMethodAttribute
        if (!namedTypeSymbol.DerivesFrom(testMethodAttributeSymbol))
        {
            return;
        }

        foreach (IMethodSymbol constructor in namedTypeSymbol.InstanceConstructors)
        {
            // Check if constructor has CallerFilePath and CallerLineNumber parameters
            bool hasCallerFilePath = false;
            bool hasCallerLineNumber = false;

            foreach (IParameterSymbol parameter in constructor.Parameters)
            {
                // Check for CallerFilePath attribute
                foreach (AttributeData attribute in parameter.GetAttributes())
                {
                    if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, callerFilePathAttributeSymbol))
                    {
                        hasCallerFilePath = true;
                        break;
                    }

                    if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, callerLineNumberAttributeSymbol))
                    {
                        hasCallerLineNumber = true;
                        break;
                    }
                }
            }

            if (!hasCallerFilePath || !hasCallerLineNumber)
            {
                context.ReportDiagnostic(constructor.CreateDiagnostic(Rule, namedTypeSymbol.Name));
            }
        }
    }
}
