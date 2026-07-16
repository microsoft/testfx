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
/// MSTEST0032: <inheritdoc cref="Resources.ReviewAlwaysTrueAssertConditionAnalyzerTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class ReviewAlwaysTrueAssertConditionAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.ReviewAlwaysTrueAssertConditionAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.ReviewAlwaysTrueAssertConditionAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.ReviewAlwaysTrueAssertConditionAnalyzerRuleId,
        Title,
        MessageFormat,
        null,
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
            IsAlwaysTrue(operation))
        {
            context.ReportDiagnostic(operation.CreateDiagnostic(Rule));
        }
    }

    private static bool IsAlwaysTrue(IInvocationOperation operation)
        => operation.TargetMethod.Name switch
        {
            "IsTrue" => AssertConditionAnalyzerHelper.GetConditionArgument(operation) is { ConstantValue: { HasValue: true, Value: true } },
            "IsFalse" => AssertConditionAnalyzerHelper.GetConditionArgument(operation) is { ConstantValue: { HasValue: true, Value: false } },
            "AreEqual" => !AssertConditionAnalyzerHelper.HasNonDefaultEqualityComparerArgument(operation)
                && (AssertConditionAnalyzerHelper.GetEqualityStatus(operation, AssertConditionAnalyzerHelper.ExpectedParameterName) == AssertConditionAnalyzerHelper.EqualityStatus.Equal
                    || AssertConditionAnalyzerHelper.HasIdenticalExpectedAndActualWithBuiltInEquality(operation, AssertConditionAnalyzerHelper.ExpectedParameterName)),
            "AreNotEqual" => !AssertConditionAnalyzerHelper.HasNonDefaultEqualityComparerArgument(operation)
                && AssertConditionAnalyzerHelper.GetEqualityStatus(operation, AssertConditionAnalyzerHelper.NotExpectedParameterName) == AssertConditionAnalyzerHelper.EqualityStatus.NotEqual,
            "AreSame" => AssertConditionAnalyzerHelper.HasIdenticalExpectedAndActual(operation, AssertConditionAnalyzerHelper.ExpectedParameterName),
            "IsNull" => AssertConditionAnalyzerHelper.GetValueArgument(operation) is { ConstantValue: { HasValue: true, Value: null } },
            "IsNotNull" => AssertConditionAnalyzerHelper.GetValueArgument(operation) is { } valueArgumentOperation && AssertConditionAnalyzerHelper.IsNotNullableType(valueArgumentOperation),
            _ => false,
        };
}
