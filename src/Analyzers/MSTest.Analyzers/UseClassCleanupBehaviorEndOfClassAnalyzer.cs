﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0034: <inheritdoc cref="Resources.UseClassCleanupBehaviorEndOfClassTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class UseClassCleanupBehaviorEndOfClassAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.UseClassCleanupBehaviorEndOfClassTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.UseClassCleanupBehaviorEndOfClassDescription), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.UseClassCleanupBehaviorEndOfClassMessageFormat), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor UseClassCleanupBehaviorEndOfClassRule = DiagnosticDescriptorHelper.Create(
        id: DiagnosticIds.UseClassCleanupBehaviorEndOfClassRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Usage,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(UseClassCleanupBehaviorEndOfClassRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingClassCleanupAttribute, out INamedTypeSymbol? classCleanupAttributeSymbol)
                 && context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestClassAttribute, out INamedTypeSymbol? testClassAttributeSymbol)
                 && context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingClassCleanupBehavior, out INamedTypeSymbol? classCleanupBehaviorSymbol))
            {
                context.RegisterSymbolAction(
                    context => AnalyzeSymbol(context, classCleanupAttributeSymbol, testClassAttributeSymbol, classCleanupBehaviorSymbol),
                    SymbolKind.Method);
            }
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol classCleanupAttributeSymbol, INamedTypeSymbol testClassAttributeSymbol, INamedTypeSymbol classCleanupBehaviorSymbol)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;

        if (!methodSymbol.ContainingType.GetAttributes().Any(x => x.AttributeClass.Inherits(testClassAttributeSymbol)))
        {
            return;
        }

        ImmutableArray<AttributeData> methodAttributes = methodSymbol.GetAttributes();
        bool hasCleanupAttr = false;
        bool hasCleanupEndOClassBehavior = false;
        foreach (AttributeData methodAttribute in methodAttributes)
        {
            if (SymbolEqualityComparer.Default.Equals(methodAttribute.AttributeClass, classCleanupAttributeSymbol))
            {
                hasCleanupAttr = true;
                foreach (TypedConstant arg in methodAttribute.ConstructorArguments)
                {
                    // one is the value for EndOFClass behavior in the CleanupBehavior enum.
                    if (SymbolEqualityComparer.Default.Equals(arg.Type, classCleanupBehaviorSymbol)
                        && 1.Equals(arg.Value))
                    {
                        hasCleanupEndOClassBehavior = true;
                    }
                }
            }
        }

        if (hasCleanupAttr && !hasCleanupEndOClassBehavior)
        {
            context.ReportDiagnostic(methodSymbol.CreateDiagnostic(UseClassCleanupBehaviorEndOfClassRule));
        }
    }
}
