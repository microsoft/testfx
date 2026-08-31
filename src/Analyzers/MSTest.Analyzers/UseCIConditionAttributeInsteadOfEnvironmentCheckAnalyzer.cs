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
/// MSTEST0080: Use '[CICondition]' attribute instead of 'Environment.GetEnvironmentVariable' checks with early return or 'Assert.Inconclusive'.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class UseCIConditionAttributeInsteadOfEnvironmentCheckAnalyzer : DiagnosticAnalyzer
{
    internal const string ConditionModeKey = nameof(ConditionModeKey);
    internal const string IncludeMode = "Include";
    internal const string ExcludeMode = "Exclude";

    /// <summary>
    /// The environment variable a null check can stand in for. Deliberately limited to the general-use 'CI' flag that
    /// every major provider sets: a guard on a provider-specific variable (say 'TF_BUILD') means "skip on Azure
    /// Pipelines", while '[CICondition]' means "skip on any CI", so replacing it would change which environments run
    /// the test.
    /// </summary>
    private const string CIEnvironmentVariable = "CI";

    private static readonly LocalizableResourceString Title = new(nameof(Resources.UseCIConditionAttributeInsteadOfEnvironmentCheckTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.UseCIConditionAttributeInsteadOfEnvironmentCheckMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.UseCIConditionAttributeInsteadOfEnvironmentCheckDescription), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.UseCIConditionAttributeInsteadOfEnvironmentCheckRuleId,
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
            if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute, out INamedTypeSymbol? testMethodAttributeSymbol) ||
                !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingConditionBaseAttribute, out INamedTypeSymbol? conditionBaseAttributeSymbol) ||
                !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingAssert, out INamedTypeSymbol? assertSymbol) ||
                !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingCIConditionAttribute, out INamedTypeSymbol? _) ||
                !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemEnvironment, out INamedTypeSymbol? environmentSymbol))
            {
                return;
            }

            IMethodSymbol? getEnvironmentVariableMethod = environmentSymbol.GetMembers("GetEnvironmentVariable")
                .OfType<IMethodSymbol>()
                .FirstOrDefault(m => m.IsStatic && m.Parameters.Length == 1);

            if (getEnvironmentVariableMethod is null)
            {
                return;
            }

            context.RegisterOperationBlockStartAction(blockContext =>
            {
                if (blockContext.OwningSymbol is not IMethodSymbol methodSymbol)
                {
                    return;
                }

                ImmutableArray<AttributeData> attributes = methodSymbol.GetAttributes();

                // Bail out when the method already carries a condition attribute. Adding a second
                // 'CIConditionAttribute' would not compile because it isn't 'AllowMultiple', and adding one next to any
                // other condition attribute can change behavior: conditions are OR-combined when they share a
                // 'GroupName' and AND-combined otherwise, and a custom attribute's 'GroupName' is an arbitrary property
                // implementation that can't be resolved statically.
                if (!attributes.Any(attr => attr.AttributeClass is not null && attr.AttributeClass.Inherits(testMethodAttributeSymbol)) ||
                    attributes.Any(attr => attr.AttributeClass is not null && attr.AttributeClass.Inherits(conditionBaseAttributeSymbol)))
                {
                    return;
                }

                ConditionGuardHelper.MethodBodyHolder methodBody = ConditionGuardHelper.RegisterMethodBodyCapture(blockContext);

                blockContext.RegisterOperationAction(
                    operationContext => AnalyzeIfStatement(operationContext, getEnvironmentVariableMethod, assertSymbol, methodBody),
                    OperationKind.Conditional);
            });
        });
    }

    private static void AnalyzeIfStatement(
        OperationAnalysisContext context,
        IMethodSymbol getEnvironmentVariableMethod,
        INamedTypeSymbol assertSymbol,
        ConditionGuardHelper.MethodBodyHolder methodBody)
    {
        var conditionalOperation = (IConditionalOperation)context.Operation;

        if (!ConditionGuardHelper.IsSkipGuard(conditionalOperation, methodBody.Body, assertSymbol))
        {
            return;
        }

        if (!TryGetCIConditionMode(conditionalOperation.Condition, getEnvironmentVariableMethod, out string? conditionMode))
        {
            return;
        }

        ImmutableDictionary<string, string?>.Builder properties = ImmutableDictionary.CreateBuilder<string, string?>();
        properties.Add(ConditionModeKey, conditionMode);

        context.ReportDiagnostic(conditionalOperation.CreateDiagnostic(
            Rule,
            properties: properties.ToImmutable()));
    }

    private static bool TryGetCIConditionMode(IOperation condition, IMethodSymbol getEnvironmentVariableMethod, [NotNullWhen(true)] out string? conditionMode)
    {
        conditionMode = null;

        if (!TryGetNullCheck(condition.WalkDownConversion(), out IOperation? checkedValue, out bool isNullCheck))
        {
            return false;
        }

        if (!IsCIEnvironmentVariableLookup(checkedValue, getEnvironmentVariableMethod))
        {
            return false;
        }

        // 'variable is null' guards bail out when not running in CI, so the test only runs in CI (include mode).
        // 'variable is not null' guards bail out when running in CI, so the test is excluded in CI.
        conditionMode = isNullCheck ? IncludeMode : ExcludeMode;
        return true;
    }

    private static bool TryGetNullCheck(IOperation condition, [NotNullWhen(true)] out IOperation? checkedValue, out bool isNullCheck)
    {
        checkedValue = null;
        isNullCheck = false;

        switch (condition)
        {
            // 'x == null' / 'x != null' (and the flipped operand order).
            case IBinaryOperation { OperatorKind: BinaryOperatorKind.Equals or BinaryOperatorKind.NotEquals } binaryOperation:
                IOperation left = binaryOperation.LeftOperand.WalkDownConversion();
                IOperation right = binaryOperation.RightOperand.WalkDownConversion();

                if (IsNullLiteral(right))
                {
                    checkedValue = left;
                }
                else if (IsNullLiteral(left))
                {
                    checkedValue = right;
                }
                else
                {
                    return false;
                }

                isNullCheck = binaryOperation.OperatorKind == BinaryOperatorKind.Equals;
                return true;

            // 'x is null' / 'x is not null'.
            case IIsPatternOperation isPatternOperation:
                checkedValue = isPatternOperation.Value.WalkDownConversion();
                return TryGetNullPattern(isPatternOperation.Pattern, out isNullCheck);

            default:
                return false;
        }
    }

    private static bool TryGetNullPattern(IPatternOperation pattern, out bool isNullCheck)
    {
        isNullCheck = false;

        switch (pattern)
        {
            case IConstantPatternOperation constantPattern when IsNullLiteral(constantPattern.Value.WalkDownConversion()):
                isNullCheck = true;
                return true;

            case INegatedPatternOperation negatedPattern:
                bool isNegatedNullCheck = TryGetNullPattern(negatedPattern.Pattern, out bool innerIsNullCheck);
                isNullCheck = !innerIsNullCheck;
                return isNegatedNullCheck;

            default:
                return false;
        }
    }

    private static bool IsNullLiteral(IOperation operation)
        => operation.ConstantValue is { HasValue: true, Value: null };

    private static bool IsCIEnvironmentVariableLookup(IOperation operation, IMethodSymbol getEnvironmentVariableMethod)
        => operation is IInvocationOperation invocation &&
            SymbolEqualityComparer.Default.Equals(invocation.TargetMethod, getEnvironmentVariableMethod) &&
            invocation.Arguments.Length == 1 &&
            invocation.Arguments[0].Value.ConstantValue is { HasValue: true, Value: CIEnvironmentVariable };
}
