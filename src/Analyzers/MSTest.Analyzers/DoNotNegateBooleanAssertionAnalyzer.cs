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
public sealed class DoNotNegateBooleanAssertionAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.DoNotNegateBooleanAssertionTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.DoNotNegateBooleanAssertionMessageFormat), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.DoNotNegateBooleanAssertionRuleId,
        Title,
        MessageFormat,
        null,
        Category.Usage,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingAssert, out var assertSymbol))
            {
                context.RegisterOperationAction(context => AnalyzeOperation(context, assertSymbol), OperationKind.Invocation);
            }
        });
    }

    private static void AnalyzeOperation(OperationAnalysisContext context, INamedTypeSymbol assertSymbol)
    {
        IInvocationOperation invocationOperation = (IInvocationOperation)context.Operation;
        if (invocationOperation.TargetMethod.Name is not "IsTrue" and not "IsFalse"
            || !SymbolEqualityComparer.Default.Equals(invocationOperation.TargetMethod.ContainingType, assertSymbol))
        {
            return;
        }

        var conditionArgument = invocationOperation.Arguments.FirstOrDefault(x => x.Parameter?.Name == "condition");
        if (conditionArgument != null
            && conditionArgument.Children.Any(op => op is IUnaryOperation unary && unary.OperatorKind == UnaryOperatorKind.Not))
        {
            context.ReportDiagnostic(invocationOperation.CreateDiagnostic(Rule));
        }
    }
}
