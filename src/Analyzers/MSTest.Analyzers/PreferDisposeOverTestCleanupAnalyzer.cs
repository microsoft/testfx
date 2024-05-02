// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class PreferDisposeOverTestCleanupAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.PreferDisposeOverTestCleanupTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.PreferDisposeOverTestCleanupMessageFormat), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.PreferDisposeOverTestCleanupRuleId,
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
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestCleanupAttribute, out INamedTypeSymbol? testCleanupAttributeSymbol))
            {
                INamedTypeSymbol? iasyncDisposableSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemIAsyncDisposable);
                context.RegisterSymbolAction(context => AnalyzeSymbol(context, testCleanupAttributeSymbol, iasyncDisposableSymbol), SymbolKind.Method);
            }
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol testCleanupAttributeSymbol,
        INamedTypeSymbol? iasyncDisposableSymbol)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;

        if (methodSymbol.IsTestCleanupMethod(testCleanupAttributeSymbol))
        {
            // We want to report only if the TestCleanup method returns void or if IAsyncDisposable is available.
            if (iasyncDisposableSymbol is not null
                || methodSymbol.ReturnsVoid)
            {
                context.ReportDiagnostic(methodSymbol.CreateDiagnostic(Rule));
            }
        }
    }
}
