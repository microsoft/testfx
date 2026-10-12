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
/// MSTEST0090: <inheritdoc cref="Resources.AvoidEnumerableAssertionsOnQueryableTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class AvoidEnumerableAssertionsOnQueryableAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.AvoidEnumerableAssertionsOnQueryableTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.AvoidEnumerableAssertionsOnQueryableMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.AvoidEnumerableAssertionsOnQueryableDescription), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.AvoidEnumerableAssertionsOnQueryableRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Performance,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(context =>
        {
            INamedTypeSymbol? assertSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingAssert);
            INamedTypeSymbol? queryableSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemLinqIQueryable);
            if (assertSymbol is null || queryableSymbol is null)
            {
                return;
            }

            INamedTypeSymbol? enumerableSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemLinqEnumerable);
            context.RegisterOperationAction(context => AnalyzeInvocation(context, assertSymbol, queryableSymbol, enumerableSymbol), OperationKind.Invocation);
        });
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context, INamedTypeSymbol assertSymbol, INamedTypeSymbol queryableSymbol, INamedTypeSymbol? enumerableSymbol)
    {
        var invocation = (IInvocationOperation)context.Operation;
        IMethodSymbol method = invocation.TargetMethod;
        if (!SymbolEqualityComparer.Default.Equals(method.ContainingType, assertSymbol))
        {
            return;
        }

        string? queryOperation = method.Name switch
        {
            "HasCount" => "Count",
            "IsEmpty" or "IsNotEmpty" => "Any",
            "ContainsSingle" when IsResultIgnored(invocation) => "Count",
            _ => null,
        };
        if (queryOperation is null)
        {
            return;
        }

        IArgumentOperation? collection = invocation.Arguments.FirstOrDefault(argument => argument.Parameter?.Name == "collection");
        if (collection is null || !GetCollectionType(collection.Value, enumerableSymbol).DerivesFrom(queryableSymbol))
        {
            return;
        }

        context.ReportDiagnostic(invocation.CreateDiagnostic(Rule, $"Assert.{method.Name}", queryOperation));
    }

    private static ITypeSymbol? GetCollectionType(IOperation operation, INamedTypeSymbol? enumerableSymbol)
    {
        while (true)
        {
            operation = operation.WalkDownBuiltInConversion();
            switch (operation)
            {
                case IParenthesizedOperation parenthesized:
                    operation = parenthesized.Operand;
                    break;
                case IInvocationOperation { TargetMethod.Name: "AsEnumerable" } invocation
                    when SymbolEqualityComparer.Default.Equals((invocation.TargetMethod.ReducedFrom ?? invocation.TargetMethod).ContainingType, enumerableSymbol)
                        && (invocation.Instance is not null || invocation.Arguments.Length == 1):
                    // AsEnumerable changes binding, but does not materialize or cache query results.
                    operation = invocation.Instance ?? invocation.Arguments[0].Value;
                    break;
                default:
                    return operation.Type;
            }
        }
    }

    private static bool IsResultIgnored(IInvocationOperation invocation)
    {
        IOperation result = invocation;
        while (result.Parent is IParenthesizedOperation or IConversionOperation { Conversion.IsUserDefined: false })
        {
            result = result.Parent;
        }

        return result.Parent is IExpressionStatementOperation
            or ISimpleAssignmentOperation { Target: IDiscardOperation };
    }
}
