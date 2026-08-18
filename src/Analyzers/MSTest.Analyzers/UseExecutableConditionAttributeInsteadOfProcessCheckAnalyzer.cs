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
/// MSTEST0083: Use '[ExecutableCondition]' attribute instead of checking a file before starting the same executable.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class UseExecutableConditionAttributeInsteadOfProcessCheckAnalyzer : DiagnosticAnalyzer
{
    internal const string ExecutableKey = nameof(ExecutableKey);

    private static readonly LocalizableResourceString Title = new(nameof(Resources.UseExecutableConditionAttributeInsteadOfProcessCheckTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.UseExecutableConditionAttributeInsteadOfProcessCheckMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.UseExecutableConditionAttributeInsteadOfProcessCheckDescription), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.UseExecutableConditionAttributeInsteadOfProcessCheckRuleId,
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
                !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingExecutableConditionAttribute, out INamedTypeSymbol? _) ||
                !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemIOFile, out INamedTypeSymbol? fileSymbol) ||
                !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemDiagnosticsProcess, out INamedTypeSymbol? processSymbol) ||
                !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemDiagnosticsProcessStartInfo, out INamedTypeSymbol? processStartInfoSymbol))
            {
                return;
            }

            IMethodSymbol? fileExistsMethod = fileSymbol.GetMembers("Exists")
                .OfType<IMethodSymbol>()
                .FirstOrDefault(m => m.IsStatic && m.Parameters is [{ Type.SpecialType: SpecialType.System_String }]);

            if (fileExistsMethod is null)
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
                if (!attributes.Any(attr => attr.AttributeClass is not null && attr.AttributeClass.Inherits(testMethodAttributeSymbol)) ||
                    attributes.Any(attr => attr.AttributeClass is not null && attr.AttributeClass.Inherits(conditionBaseAttributeSymbol)))
                {
                    return;
                }

                ConditionGuardHelper.MethodBodyHolder methodBody = ConditionGuardHelper.RegisterMethodBodyCapture(blockContext);

                blockContext.RegisterOperationAction(
                    operationContext => AnalyzeIfStatement(
                        operationContext,
                        fileExistsMethod,
                        processSymbol,
                        processStartInfoSymbol,
                        assertSymbol,
                        methodBody),
                    OperationKind.Conditional);
            });
        });
    }

    private static void AnalyzeIfStatement(
        OperationAnalysisContext context,
        IMethodSymbol fileExistsMethod,
        INamedTypeSymbol processSymbol,
        INamedTypeSymbol processStartInfoSymbol,
        INamedTypeSymbol assertSymbol,
        ConditionGuardHelper.MethodBodyHolder methodBody)
    {
        var conditionalOperation = (IConditionalOperation)context.Operation;

        if (!ConditionGuardHelper.IsSkipGuard(conditionalOperation, methodBody.Body, assertSymbol) ||
            !TryGetMissingExecutableCheck(conditionalOperation.Condition, fileExistsMethod, out string? executable) ||
            conditionalOperation.Parent is not IBlockOperation block ||
            !HasMatchingProcessStartAfterGuard(block, conditionalOperation, processSymbol, processStartInfoSymbol, executable))
        {
            return;
        }

        ImmutableDictionary<string, string?>.Builder properties = ImmutableDictionary.CreateBuilder<string, string?>();
        properties.Add(ExecutableKey, executable);

        context.ReportDiagnostic(conditionalOperation.CreateDiagnostic(
            Rule,
            properties.ToImmutable(),
            executable));
    }

    private static bool TryGetMissingExecutableCheck(
        IOperation condition,
        IMethodSymbol fileExistsMethod,
        [NotNullWhen(true)] out string? executable)
    {
        condition = condition.WalkDownConversion();

        if (condition is IUnaryOperation { OperatorKind: UnaryOperatorKind.Not } unaryOperation)
        {
            return TryGetExecutableFromFileExists(unaryOperation.Operand, fileExistsMethod, out executable);
        }

        if (condition is IBinaryOperation { OperatorKind: BinaryOperatorKind.Equals or BinaryOperatorKind.NotEquals } binaryOperation)
        {
            if (TryGetBooleanConstant(binaryOperation.RightOperand, out bool rightValue) &&
                IsMissingComparison(binaryOperation.OperatorKind, rightValue))
            {
                return TryGetExecutableFromFileExists(binaryOperation.LeftOperand, fileExistsMethod, out executable);
            }

            if (TryGetBooleanConstant(binaryOperation.LeftOperand, out bool leftValue) &&
                IsMissingComparison(binaryOperation.OperatorKind, leftValue))
            {
                return TryGetExecutableFromFileExists(binaryOperation.RightOperand, fileExistsMethod, out executable);
            }
        }

        if (condition is IIsPatternOperation
            {
                Value: { } value,
                Pattern: IConstantPatternOperation { Value.ConstantValue: { HasValue: true, Value: false } },
            })
        {
            return TryGetExecutableFromFileExists(value, fileExistsMethod, out executable);
        }

        executable = null;
        return false;
    }

    private static bool IsMissingComparison(BinaryOperatorKind operatorKind, bool comparedValue)
        => operatorKind == BinaryOperatorKind.Equals ? !comparedValue : comparedValue;

    private static bool TryGetBooleanConstant(IOperation operation, out bool value)
    {
        if (operation.WalkDownConversion().ConstantValue is { HasValue: true, Value: bool constant })
        {
            value = constant;
            return true;
        }

        value = false;
        return false;
    }

    private static bool TryGetExecutableFromFileExists(
        IOperation operation,
        IMethodSymbol fileExistsMethod,
        [NotNullWhen(true)] out string? executable)
    {
        operation = operation.WalkDownConversion();
        if (operation is IInvocationOperation invocation &&
            SymbolEqualityComparer.Default.Equals(invocation.TargetMethod, fileExistsMethod) &&
            invocation.Arguments is [{ Value.ConstantValue: { HasValue: true, Value: string constant } }] &&
            constant.Length > 0)
        {
            executable = constant;
            return true;
        }

        executable = null;
        return false;
    }

    private static bool HasMatchingProcessStartAfterGuard(
        IBlockOperation block,
        IConditionalOperation guard,
        INamedTypeSymbol processSymbol,
        INamedTypeSymbol processStartInfoSymbol,
        string executable)
    {
        int guardIndex = block.Operations.IndexOf(guard);
        for (int i = guardIndex + 1; i < block.Operations.Length; i++)
        {
            if (ContainsMatchingProcessStart(block.Operations[i], processSymbol, processStartInfoSymbol, executable))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsMatchingProcessStart(
        IOperation operation,
        INamedTypeSymbol processSymbol,
        INamedTypeSymbol processStartInfoSymbol,
        string executable)
    {
        bool isMatchingProcessStart = operation is IInvocationOperation invocation &&
            invocation.TargetMethod is { IsStatic: true, Name: "Start", ContainingType: { } containingType } &&
            SymbolEqualityComparer.Default.Equals(containingType, processSymbol) &&
            TryGetArgumentValueForParameterOrdinal(invocation.Arguments, 0, out IOperation? processStartArgument) &&
            TryGetProcessStartExecutable(processStartArgument, processStartInfoSymbol, out string? startedExecutable) &&
            startedExecutable == executable;

        return isMatchingProcessStart
            || (operation is not (IAnonymousFunctionOperation or ILocalFunctionOperation)
                && operation.Children.Any(child =>
                    ContainsMatchingProcessStart(child, processSymbol, processStartInfoSymbol, executable)));
    }

    private static bool TryGetProcessStartExecutable(
        IOperation operation,
        INamedTypeSymbol processStartInfoSymbol,
        [NotNullWhen(true)] out string? executable)
    {
        operation = operation.WalkDownConversion();
        if (operation.ConstantValue is { HasValue: true, Value: string constant })
        {
            executable = constant;
            return true;
        }

        if (operation is IObjectCreationOperation objectCreation &&
            SymbolEqualityComparer.Default.Equals(objectCreation.Type, processStartInfoSymbol))
        {
            IOperation? fileNameInitializer = objectCreation.Initializer?.Initializers
                .OfType<ISimpleAssignmentOperation>()
                .FirstOrDefault(assignment =>
                    assignment.Target is IPropertyReferenceOperation { Property.Name: "FileName", Property.ContainingType: { } containingType } &&
                    SymbolEqualityComparer.Default.Equals(containingType, processStartInfoSymbol))
                ?.Value;

            if (fileNameInitializer is not null)
            {
                return TryGetProcessStartExecutable(fileNameInitializer, processStartInfoSymbol, out executable);
            }

            if (TryGetArgumentValueForParameterOrdinal(objectCreation.Arguments, 0, out IOperation? constructorFileName))
            {
                return TryGetProcessStartExecutable(constructorFileName, processStartInfoSymbol, out executable);
            }
        }

        executable = null;
        return false;
    }

    private static bool TryGetArgumentValueForParameterOrdinal(
        ImmutableArray<IArgumentOperation> arguments,
        int ordinal,
        [NotNullWhen(true)] out IOperation? argumentValue)
    {
        argumentValue = arguments.FirstOrDefault(argument => argument.Parameter?.Ordinal == ordinal)?.Value;
        return argumentValue is not null;
    }
}
