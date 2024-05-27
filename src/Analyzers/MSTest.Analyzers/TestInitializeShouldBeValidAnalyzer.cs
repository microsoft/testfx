﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class TestInitializeShouldBeValidAnalyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.TestInitializeShouldBeValidRuleId,
        new LocalizableResourceString(nameof(Resources.TestInitializeShouldBeValidTitle), Resources.ResourceManager, typeof(Resources)),
        new LocalizableResourceString(nameof(Resources.TestInitializeShouldBeValidMessageFormat), Resources.ResourceManager, typeof(Resources)),
        new LocalizableResourceString(nameof(Resources.TestInitializeShouldBeValidDescription), Resources.ResourceManager, typeof(Resources)),
        Category.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestInitializeAttribute, out INamedTypeSymbol? testInitializeAttributeSymbol))
            {
                INamedTypeSymbol? taskSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTask);
                INamedTypeSymbol? valueTaskSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksValueTask);
                bool canDiscoverInternals = context.Compilation.CanDiscoverInternals();
                context.RegisterSymbolAction(
                    context => AnalyzeSymbol(context, testInitializeAttributeSymbol, taskSymbol, valueTaskSymbol, canDiscoverInternals),
                    SymbolKind.Method);
            }
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol testInitializeAttributeSymbol, INamedTypeSymbol? taskSymbol,
        INamedTypeSymbol? valueTaskSymbol, bool canDiscoverInternals)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;
        if (methodSymbol.IsTestInitializeMethod(testInitializeAttributeSymbol)
            && !methodSymbol.HasValidFixtureMethodSignature(taskSymbol, valueTaskSymbol, canDiscoverInternals, shouldBeStatic: false,
                allowGenericType: false, testContextSymbol: null, out bool isFixable))
        {
            context.ReportDiagnostic(isFixable
                ? methodSymbol.CreateDiagnostic(Rule, methodSymbol.Name)
                : methodSymbol.CreateDiagnostic(Rule, DiagnosticDescriptorHelper.CannotFixProperties, methodSymbol.Name));
        }
    }
}
