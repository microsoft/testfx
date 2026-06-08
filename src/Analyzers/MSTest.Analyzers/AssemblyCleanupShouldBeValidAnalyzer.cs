// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0013: <inheritdoc cref="Resources.AssemblyCleanupShouldBeValidTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class AssemblyCleanupShouldBeValidAnalyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.AssemblyCleanupShouldBeValidRuleId,
        new LocalizableResourceString(nameof(Resources.AssemblyCleanupShouldBeValidTitle), Resources.ResourceManager, typeof(Resources)),
        new LocalizableResourceString(nameof(Resources.AssemblyCleanupShouldBeValidMessageFormat), Resources.ResourceManager, typeof(Resources)),
        new LocalizableResourceString(nameof(Resources.AssemblyCleanupShouldBeValidDescription), Resources.ResourceManager, typeof(Resources)),
        Category.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (FixtureMethodAnalyzerHelper.TryGetFixtureMethodSymbols(context.Compilation, WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingAssemblyCleanupAttribute, out FixtureMethodAnalyzerHelper.FixtureMethodSymbols symbols))
            {
                INamedTypeSymbol? testContextSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestContext);
                context.RegisterSymbolAction(
                    symbolContext => AnalyzeSymbol(symbolContext, symbols.FixtureAttributeSymbol, symbols.TestClassAttributeSymbol, symbols.TaskSymbol, symbols.ValueTaskSymbol, testContextSymbol, symbols.CanDiscoverInternals),
                    SymbolKind.Method);
            }
        });
    }

    private static void AnalyzeSymbol(
        SymbolAnalysisContext context,
        INamedTypeSymbol assemblyCleanupAttributeSymbol,
        INamedTypeSymbol testClassAttributeSymbol,
        INamedTypeSymbol? taskSymbol,
        INamedTypeSymbol? valueTaskSymbol,
        INamedTypeSymbol? testContextSymbol,
        bool canDiscoverInternals)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;

        if (!methodSymbol.HasAttribute(assemblyCleanupAttributeSymbol))
        {
            return;
        }

        if (!methodSymbol.HasValidFixtureMethodSignature(taskSymbol, valueTaskSymbol, canDiscoverInternals, shouldBeStatic: true, allowGenericType: false, FixtureParameterMode.OptionalTestContext, testContextSymbol, testClassAttributeSymbol, fixtureAllowInheritedTestClass: false, out bool isFixable))
        {
            context.ReportDiagnostic(isFixable
                ? methodSymbol.CreateDiagnostic(Rule, methodSymbol.Name)
                : methodSymbol.CreateDiagnostic(Rule, DiagnosticDescriptorHelper.CannotFixProperties, methodSymbol.Name));
        }
    }
}
