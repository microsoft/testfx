// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class UseNullableForIsNullAndIsNotNullAssertionsAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.UseNullableForIsNullAndIsNotNullAssertionsAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.UseNullableForIsNullAndIsNotNullAssertionsAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.UseNullableForIsNullAndIsNotNullAssertionsAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor UseNullableForIsNullAndIsNotNullAssertionsAnalyzerRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.UseNullableForIsNullAndIsNotNullAssertionsAnalyzerRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Usage,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(UseNullableForIsNullAndIsNotNullAssertionsAnalyzerRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingAssert, out INamedTypeSymbol? assertSymbol)
                && context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemNullable, out INamedTypeSymbol? nullableSymbol))
            {
                context.RegisterOperationAction(context => AnalyzeOperation(context, assertSymbol, nullableSymbol), OperationKind.Invocation);
            }
        });
    }

    private static void AnalyzeOperation(OperationAnalysisContext context, INamedTypeSymbol assertSymbol, INamedTypeSymbol? nullableSymbol)
    {
        var invocationOperation = (IInvocationOperation)context.Operation;

        if ((invocationOperation.TargetMethod.Name != "IsNull" && invocationOperation.TargetMethod.Name != "IsNotNull")
            || !SymbolEqualityComparer.Default.Equals(assertSymbol, invocationOperation.TargetMethod.ContainingType))
        {
            return;
        }

        foreach (IArgumentOperation argument in invocationOperation.Arguments)
        {
            if (SymbolEqualityComparer.Default.Equals(argument.Type, nullableSymbol) && argument.Parameter?.Name == "value") // not working.
            {
                context.ReportDiagnostic(invocationOperation.CreateDiagnostic(UseNullableForIsNullAndIsNotNullAssertionsAnalyzerRule));
                break;
            }
        }
    }
}
