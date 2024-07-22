// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0034: <inheritdoc cref="Resources.SetClassCleanupEndOFClassBehaviorTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class SetClassCleanupEndOFClassBehaviorAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.SetClassCleanupEndOFClassBehaviorTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.SetClassCleanupEndOFClassBehaviorDescription), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.SetClassCleanupEndOFClassBehaviorMessageFormat), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor SetClassCleanupEndOFClassBehaviorRule = DiagnosticDescriptorHelper.Create(
        id: DiagnosticIds.SetClassCleanupEndOFClassBehaviorRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Usage,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(SetClassCleanupEndOFClassBehaviorRule);

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

        if (!methodSymbol.ContainingType.GetAttributes().Any(x => x.AttributeClass.Inherits(testClassAttributeSymbol))
            || !methodSymbol.IsClassCleanupMethod(classCleanupAttributeSymbol))
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
                    if (SymbolEqualityComparer.Default.Equals(arg.Type, classCleanupBehaviorSymbol)
                        && arg.Value!.Equals(1))
                    {
                        hasCleanupEndOClassBehavior = true;
                    }
                }
            }
        }

        if (hasCleanupAttr && !hasCleanupEndOClassBehavior)
        {
            context.ReportDiagnostic(methodSymbol.CreateDiagnostic(SetClassCleanupEndOFClassBehaviorRule));
        }
    }
}
