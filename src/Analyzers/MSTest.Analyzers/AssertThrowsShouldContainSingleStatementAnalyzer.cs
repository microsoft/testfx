// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0050: Assert.Throws should contain only a single statement/expression.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
internal sealed class AssertThrowsShouldContainSingleStatementAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.AssertThrowsShouldContainSingleStatementTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.AssertThrowsShouldContainSingleStatementMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.AssertThrowsShouldContainSingleStatementDescription), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.AssertThrowsShouldContainSingleStatementRuleId,
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
            if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingAssert, out INamedTypeSymbol? assertTypeSymbol))
            {
                return;
            }

            context.RegisterOperationAction(context => AnalyzeInvocationOperation(context, assertTypeSymbol), OperationKind.Invocation);
        });
    }

    private static void AnalyzeInvocationOperation(OperationAnalysisContext context, INamedTypeSymbol assertTypeSymbol)
    {
        var operation = (IInvocationOperation)context.Operation;
        IMethodSymbol targetMethod = operation.TargetMethod;

        if (!SymbolEqualityComparer.Default.Equals(targetMethod.ContainingType, assertTypeSymbol))
        {
            return;
        }

        // Check if it's one of the Assert.Throws methods
        if (targetMethod.Name is not ("Throws" or "ThrowsExactly" or "ThrowsAsync" or "ThrowsExactlyAsync"))
        {
            return;
        }

        // Find the action parameter (lambda expression) - it's typically the first parameter
        foreach (IArgumentOperation argument in operation.Arguments)
        {
            if (argument.Value is IDelegateCreationOperation delegateCreation
                && delegateCreation.Target is IAnonymousFunctionOperation delegateLambda)
            {
                // Check if this parameter is likely the action parameter by checking its type
                if (IsActionParameter(argument.Parameter))
                {
                    AnalyzeLambdaBody(context, delegateLambda, operation);
                    break;
                }
            }
        }
    }

    private static bool IsActionParameter(IParameterSymbol? parameter)
    {
        if (parameter is null)
        {
            return false;
        }

        // The action parameter is typically named "action" or is a delegate type
        return parameter.Name == "action" ||
               parameter.Type.TypeKind == TypeKind.Delegate;
    }

    private static void AnalyzeLambdaBody(OperationAnalysisContext context, IAnonymousFunctionOperation lambdaOperation, IInvocationOperation invocationOperation)
    {
        if (lambdaOperation.Body is IBlockOperation blockOperation)
        {
            // Count meaningful statements (excluding implicit return statements and empty statements)
            int statementCount = 0;
            foreach (IOperation statement in blockOperation.Operations)
            {
                // Skip implicit return statements (they don't represent user code)
                if (statement is IReturnOperation returnOp && returnOp.IsImplicit)
                {
                    continue;
                }

                // Skip empty statements
                if (statement is IEmptyOperation)
                {
                    continue;
                }

                statementCount++;
            }

            // Flag if there are multiple meaningful statements
            if (statementCount > 1)
            {
                context.ReportDiagnostic(invocationOperation.CreateDiagnostic(Rule));
            }
        }

        // For expression-bodied lambdas (not block lambdas), they always have a single expression, so no need to flag
    }
}
