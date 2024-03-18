// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class PreferConstructorOverTestInitializeAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.PreferConstructorOverTestInitializeTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.PreferConstructorOverTestInitializeMessageFormat), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.PreferConstructorOverTestInitializeRuleId,
        Title,
        MessageFormat,
        null,
        Category.Design,
        DiagnosticSeverity.Info,
        isEnabledByDefault: false);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestInitializeAttribute, out var testInitAttributeSymbol))
            {
                context.RegisterSymbolAction(context => AnalyzeSymbol(context, testInitAttributeSymbol), SymbolKind.Method);
            }
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol testInitAttributeSymbol)
    {
        IMethodSymbol methodSymbol = (IMethodSymbol)context.Symbol;

        if (methodSymbol.IsTestInitializeMethod(testInitAttributeSymbol) && methodSymbol.ReturnsVoid)
        {
            context.ReportDiagnostic(methodSymbol.CreateDiagnostic(Rule));
        }
    }
}
