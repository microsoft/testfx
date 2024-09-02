// Copyright (c) Microsoft Corporation. All rights reserved.
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
                 && context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingClassCleanupBehavior, out INamedTypeSymbol? classCleanupBehaviorSymbol)
                 && context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingClassCleanupExecutionAttribute, out INamedTypeSymbol? classCleanupExecutionAttributeSymbol))
            {
                context.RegisterSymbolAction(
                    context => AnalyzeSymbol(context, classCleanupAttributeSymbol, testClassAttributeSymbol, classCleanupBehaviorSymbol, classCleanupExecutionAttributeSymbol),
                    SymbolKind.Method);
            }
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol classCleanupAttributeSymbol, INamedTypeSymbol testClassAttributeSymbol, INamedTypeSymbol classCleanupBehaviorSymbol, INamedTypeSymbol classCleanupExecutionAttributeSymbol)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;

        if (!methodSymbol.ContainingType.GetAttributes().Any(x => x.AttributeClass.Inherits(testClassAttributeSymbol)))
        {
            return;
        }

        // Check if the assembly has the ClassCleanupExecutionAttribute with EndOfClass behavior
        bool assemblyHasEndOfClassCleanup = context.Compilation.Assembly
            .GetAttributes()
            .Any(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, classCleanupExecutionAttributeSymbol)
                && attr.ConstructorArguments.Length == 1
                && SymbolEqualityComparer.Default.Equals(attr.ConstructorArguments[0].Type, classCleanupBehaviorSymbol)
                && 1.Equals(attr.ConstructorArguments[0].Value));

        ImmutableArray<AttributeData> methodAttributes = methodSymbol.GetAttributes();
        bool hasCleanupAttr = false;
        bool hasCleanupEndOClassBehavior = false;
        bool hasClassCleanupBehavior = false;

        foreach (AttributeData methodAttribute in methodAttributes)
        {
            if (SymbolEqualityComparer.Default.Equals(methodAttribute.AttributeClass, classCleanupAttributeSymbol))
            {
                hasCleanupAttr = true;
                foreach (TypedConstant arg in methodAttribute.ConstructorArguments)
                {
                    if (SymbolEqualityComparer.Default.Equals(arg.Type, classCleanupBehaviorSymbol))
                    {
                        hasClassCleanupBehavior = true;

                        // one is the value for EndOFClass behavior in the CleanupBehavior enum.
                        if (1.Equals(arg.Value))
                        {
                            hasCleanupEndOClassBehavior = true;
                        }
                    }
                }
            }
        }

        if (!hasCleanupAttr ||
            (!hasClassCleanupBehavior && assemblyHasEndOfClassCleanup)
            || (hasClassCleanupBehavior && hasCleanupEndOClassBehavior))
        {
            return;
        }

        context.ReportDiagnostic(methodSymbol.CreateDiagnostic(UseClassCleanupBehaviorEndOfClassRule));
    }
}
