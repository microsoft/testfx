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
public sealed class AssertionArgsShouldAvoidConditionalAccessAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableArray<string> SupportedMethodNames = ImmutableArray.Create(new[]
    {
        "AreEqual",
        "AreNotEqual",
        "IsTrue",
        "IsFalse",
    });

    private static readonly LocalizableResourceString Title = new(nameof(Resources.AssertionArgsShouldAvoidConditionalAccessTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.AssertionArgsShouldAvoidConditionalAccessDescription), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.AssertionArgsShouldAvoidConditionalAccessMessageFormat), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.AssertionArgsShouldAvoidConditionalAccessRuleId,
        Title,
        MessageFormat,
        Description,
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
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingAssert, out INamedTypeSymbol? assertSymbol))
            {
                context.RegisterOperationAction(context => AnalyzeOperation(context, assertSymbol), OperationKind.Invocation);
            }
        });
    }

    private static void AnalyzeOperation(OperationAnalysisContext context, INamedTypeSymbol assertSymbol)
    {
        var invocationOperation = (IInvocationOperation)context.Operation;

        // This is not an invocation of the expected assertion methods.
        if (!SupportedMethodNames.Contains(invocationOperation.TargetMethod.Name)
            || !SymbolEqualityComparer.Default.Equals(assertSymbol, invocationOperation.TargetMethod.ContainingType)
            || !FindConditionalArguments(invocationOperation))
        {
            return;
        }

        context.ReportDiagnostic(invocationOperation.CreateDiagnostic(Rule));
    }

    private static bool FindConditionalArguments(IInvocationOperation invocationOperation)
    {
        foreach (IArgumentOperation argument in invocationOperation.Arguments)
        {
            // Check for conditional access
            //      a?.b
            //      a?.b?.c
            //      a.b?.c
            if (argument.Value.Kind == OperationKind.ConditionalAccess)
            {
                return true;
            }

            // Check for binary operations with conditional access => s?.Length > 1.
            if (argument.Value is IBinaryOperation binaryOperation)
            {
                if (binaryOperation.LeftOperand.Kind == OperationKind.ConditionalAccess || binaryOperation.RightOperand.Kind == OperationKind.ConditionalAccess)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
