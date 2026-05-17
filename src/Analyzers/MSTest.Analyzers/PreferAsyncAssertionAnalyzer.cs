// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

using MSTest.Analyzers.Helpers;
using MSTest.Analyzers.RoslynAnalyzerHelpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0064: <inheritdoc cref="Resources.PreferAsyncAssertionTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class PreferAsyncAssertionAnalyzer : DiagnosticAnalyzer
{
    internal const string GetAwaiterMethodName = "GetAwaiter";
    internal const string GetResultMethodName = "GetResult";

    private static readonly LocalizableResourceString Title = new(nameof(Resources.PreferAsyncAssertionTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.PreferAsyncAssertionDescription), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.PreferAsyncAssertionMessageFormat), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.PreferAsyncAssertionRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Usage,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingAssert, out INamedTypeSymbol? assertSymbol) ||
                !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute, out INamedTypeSymbol? testMethodAttributeSymbol) ||
                !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTask, out INamedTypeSymbol? taskSymbol))
            {
                return;
            }

            context.RegisterOperationAction(
                context => AnalyzeInvocation(context, assertSymbol, testMethodAttributeSymbol, taskSymbol),
                OperationKind.Invocation);
        });
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context, INamedTypeSymbol assertSymbol, INamedTypeSymbol testMethodAttributeSymbol, INamedTypeSymbol taskSymbol)
    {
        var operation = (IInvocationOperation)context.Operation;
        IMethodSymbol targetMethod = operation.TargetMethod;
        if (
            !SymbolEqualityComparer.Default.Equals(targetMethod.ContainingType, assertSymbol) ||
            targetMethod.Name is not ("Throws" or "ThrowsExactly") ||
            context.ContainingSymbol is not IMethodSymbol containingMethod ||
            !containingMethod.GetAttributes().Any(attr => attr.AttributeClass.Inherits(testMethodAttributeSymbol)) ||
            IsInsideUnsupportedAwaitContext(operation) ||
            !TryGetActionArgument(operation, out IArgumentOperation? actionArgument) ||
            !TryGetBlockedTaskOperationFromArgument(actionArgument.Value, out IOperation? asyncOperation))
        {
            return;
        }

        ITypeSymbol? asyncExpressionType = asyncOperation.Type;
        if (asyncExpressionType is null || !context.Compilation.ClassifyCommonConversion(asyncExpressionType, taskSymbol).IsImplicit)
        {
            return;
        }

        context.ReportDiagnostic(operation.CreateDiagnostic(Rule, targetMethod.Name + "Async", targetMethod.Name));
    }

    private static bool TryGetActionArgument(IInvocationOperation operation, [NotNullWhen(true)] out IArgumentOperation? actionArgument)
    {
        foreach (IArgumentOperation argument in operation.Arguments)
        {
            if (argument.Parameter?.Name == "action")
            {
                actionArgument = argument;
                return true;
            }
        }

        actionArgument = null;
        return false;
    }

    private static bool IsInsideUnsupportedAwaitContext(IOperation operation)
    {
        for (IOperation? current = operation.Parent; current is not null; current = current.Parent)
        {
            if (current is IAnonymousFunctionOperation or ILocalFunctionOperation ||
                current.Kind == OperationKind.Lock ||
                (current.Parent is ICatchClauseOperation catchClauseOperation && ReferenceEquals(catchClauseOperation.Filter, current)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetBlockedTaskOperationFromArgument(IOperation argumentValueOperation, [NotNullWhen(true)] out IOperation? asyncOperation)
    {
        if (argumentValueOperation.WalkDownConversion() is not IDelegateCreationOperation delegateCreationOperation ||
            delegateCreationOperation.Target is not IAnonymousFunctionOperation anonymousFunctionOperation ||
            !TryGetSingleOperation(anonymousFunctionOperation.Body, out IOperation? lambdaOperation))
        {
            asyncOperation = null;
            return false;
        }

        return TryGetBlockedTaskOperation(lambdaOperation, out asyncOperation);
    }

    private static bool TryGetSingleOperation(IBlockOperation blockOperation, [NotNullWhen(true)] out IOperation? operation)
    {
        operation = null;

        foreach (IOperation childOperation in blockOperation.Operations)
        {
            IOperation? candidateOperation = childOperation switch
            {
                IExpressionStatementOperation expressionStatementOperation => expressionStatementOperation.Operation,

                // For any return-with-value, treat the returned value as the candidate, except for the
                // synthetic implicit return that VB 'Function' lambdas emit: it returns an implicit
                // ILocalReferenceOperation to the function-name local. Treating that as a candidate
                // would cause single-statement VB Function lambdas to look as if they contain
                // multiple operations, and the diagnostic would be missed.
                IReturnOperation { ReturnedValue: { } returnedValue }
                    => GetExplicitReturnedValue(returnedValue),

                // Implicit return with no value contributes no candidate (e.g. end of void lambda).
                IReturnOperation => null,

                // Skip any other compiler-synthesized operation (e.g. VB exit-function labels).
                _ when childOperation.IsImplicit => null,

                _ => childOperation,
            };

            if (candidateOperation is null)
            {
                continue;
            }

            if (operation is not null)
            {
                operation = null;
                return false;
            }

            operation = candidateOperation;
        }

        return operation is not null;
    }

    private static IOperation? GetExplicitReturnedValue(IOperation returnedValue)
    {
        // The synthetic implicit return that VB Function lambdas emit returns the (also implicit)
        // function-name local. Detect that case via the unwrapped operation.
        IOperation unwrapped = returnedValue.WalkDownConversion();
        return unwrapped is ILocalReferenceOperation { IsImplicit: true }
            ? null
            : returnedValue;
    }

    private static bool TryGetBlockedTaskOperation(IOperation operation, [NotNullWhen(true)] out IOperation? asyncOperation)
    {
        if (operation.WalkDownConversion() is IInvocationOperation getResultInvocation &&
            getResultInvocation.Arguments.Length == 0 &&
            getResultInvocation.TargetMethod.Name == GetResultMethodName &&
            getResultInvocation.Instance?.WalkDownConversion() is IInvocationOperation getAwaiterInvocation &&
            getAwaiterInvocation.Arguments.Length == 0 &&
            getAwaiterInvocation.TargetMethod.Name == GetAwaiterMethodName &&
            getAwaiterInvocation.Instance is { } instance)
        {
            asyncOperation = instance.WalkDownConversion();
            return true;
        }

        asyncOperation = null;
        return false;
    }
}
