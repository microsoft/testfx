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
/// MSTEST0051: Assert.Throws should contain only a single statement/expression.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class AssertThrowsShouldContainSingleStatementAnalyzer : DiagnosticAnalyzer
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

        if (!SymbolEqualityComparer.Default.Equals(targetMethod.ContainingType, assertTypeSymbol) ||
            !targetMethod.Name.StartsWith("Throws", StringComparison.Ordinal))
        {
            return;
        }

        // Find the action parameter (lambda expression) - it's typically the first parameter
        foreach (IArgumentOperation argument in operation.Arguments)
        {
            if (argument.Parameter?.Ordinal == 0)
            {
                AnalyzeActionArgument(context, argument.Value.WalkDownConversion(), operation);
                break;
            }
        }
    }

    private static void AnalyzeActionArgument(OperationAnalysisContext context, IOperation argumentValueOperation, IInvocationOperation invocationOperation)
    {
        if (argumentValueOperation is not IDelegateCreationOperation delegateCreation ||
            delegateCreation.Target is not IAnonymousFunctionOperation lambdaOperation ||
            lambdaOperation.Body is not IBlockOperation blockOperation)
        {
            return;
        }

        // Count meaningful statements (excluding implicit return statements and empty statements)
        int statementCount = 0;
        foreach (IOperation statement in blockOperation.Operations)
        {
            // Skip implicit return statements (they don't represent user code)
            if (statement is IReturnOperation { IsImplicit: true })
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
}
