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
/// MSTEST0025: <inheritdoc cref="Resources.PreferAssertFailOverAlwaysFalseConditionsTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class PreferAssertFailOverAlwaysFalseConditionsAnalyzer : DiagnosticAnalyzer
{
    private const string MessageParameterName = "message";

    private static readonly LocalizableResourceString Title = new(nameof(Resources.PreferAssertFailOverAlwaysFalseConditionsTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.PreferAssertFailOverAlwaysFalseConditionsMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.PreferAssertFailOverAlwaysFalseConditionsDescription), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.PreferAssertFailOverAlwaysFalseConditionsRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Design,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

        context.RegisterCompilationStartAction(context =>
        {
            Compilation compilation = context.Compilation;
            INamedTypeSymbol? assertSymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingAssert);
            if (assertSymbol is not null)
            {
                context.RegisterOperationAction(context => AnalyzeOperation(context, assertSymbol), OperationKind.Invocation);
            }
        });
    }

    private static void AnalyzeOperation(OperationAnalysisContext context, INamedTypeSymbol assertSymbol)
    {
        var operation = (IInvocationOperation)context.Operation;

        if (assertSymbol.Equals(operation.TargetMethod.ContainingType, SymbolEqualityComparer.Default) &&
            IsAlwaysFalse(operation))
        {
            // Assert.Fail returns void, so it cannot replace a result used by another expression.
            ImmutableDictionary<string, string?>? properties = operation.TargetMethod.ReturnsVoid || operation.Parent is IExpressionStatementOperation
                ? null
                : DiagnosticDescriptorHelper.CannotFixProperties;
            context.ReportDiagnostic(operation.CreateDiagnostic(Rule, GetAdditionalLocations(operation), properties, operation.TargetMethod.Name));
        }
    }

    private static ImmutableArray<Location> GetAdditionalLocations(IInvocationOperation operation)
    {
        IArgumentOperation? messageArg = operation.Arguments.FirstOrDefault(arg => arg.Parameter?.Name == MessageParameterName && arg.ArgumentKind != ArgumentKind.DefaultValue);
        return messageArg is null
            ? ImmutableArray<Location>.Empty
            : ImmutableArray.Create(messageArg.Syntax.GetLocation());
    }

    private static bool IsAlwaysFalse(IInvocationOperation operation)
        => operation.TargetMethod.Name switch
        {
            "IsTrue" => AssertConditionAnalyzerHelper.GetConditionArgument(operation) is { ConstantValue: { HasValue: true, Value: false } },
            "IsFalse" => AssertConditionAnalyzerHelper.GetConditionArgument(operation) is { ConstantValue: { HasValue: true, Value: true } },
            "AreEqual" => !AssertConditionAnalyzerHelper.HasNonDefaultEqualityComparerArgument(operation)
                && AssertConditionAnalyzerHelper.GetEqualityStatus(operation, AssertConditionAnalyzerHelper.ExpectedParameterName) == AssertConditionAnalyzerHelper.EqualityStatus.NotEqual,
            "AreNotEqual" => !AssertConditionAnalyzerHelper.HasNonDefaultEqualityComparerArgument(operation)
                && (AssertConditionAnalyzerHelper.GetEqualityStatus(operation, AssertConditionAnalyzerHelper.NotExpectedParameterName) == AssertConditionAnalyzerHelper.EqualityStatus.Equal
                    || AssertConditionAnalyzerHelper.HasIdenticalExpectedAndActualWithBuiltInEquality(operation, AssertConditionAnalyzerHelper.NotExpectedParameterName)),
            "AreNotSame" => AssertConditionAnalyzerHelper.HasIdenticalExpectedAndActual(operation, AssertConditionAnalyzerHelper.NotExpectedParameterName),
            "IsNotNull" => AssertConditionAnalyzerHelper.GetValueArgument(operation) is { ConstantValue: { HasValue: true, Value: null } },
            "IsNull" => AssertConditionAnalyzerHelper.GetValueArgument(operation) is { } valueArgumentOperation && AssertConditionAnalyzerHelper.IsNotNullableType(valueArgumentOperation),
            "IsExactInstanceOfType" => operation.TargetMethod.TypeArguments is [{ IsAbstract: true }]
                || IsAbstractTypeOf(operation.Arguments.FirstOrDefault(arg => arg.Parameter?.Name == "expectedType")?.Value),
            _ => false,
        };

    private static bool IsAbstractTypeOf(IOperation? operation)
        => operation switch
        {
            ITypeOfOperation { TypeOperand.IsAbstract: true } => true,
            IParenthesizedOperation parenthesized => IsAbstractTypeOf(parenthesized.Operand),
            IConversionOperation { Conversion.IsUserDefined: false } conversion => IsAbstractTypeOf(conversion.Operand),
            _ => false,
        };
}
