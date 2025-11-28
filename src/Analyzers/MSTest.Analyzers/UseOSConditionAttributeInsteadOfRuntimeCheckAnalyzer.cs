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
/// MSTEST0059: Use '[OSCondition]' attribute instead of 'RuntimeInformation.IsOSPlatform' calls with early return or 'Assert.Inconclusive'.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class UseOSConditionAttributeInsteadOfRuntimeCheckAnalyzer : DiagnosticAnalyzer
{
    internal const string IsNegatedKey = nameof(IsNegatedKey);
    internal const string OSPlatformKey = nameof(OSPlatformKey);

    private static readonly LocalizableResourceString Title = new(nameof(Resources.UseOSConditionAttributeInsteadOfRuntimeCheckTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.UseOSConditionAttributeInsteadOfRuntimeCheckMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.UseOSConditionAttributeInsteadOfRuntimeCheckDescription), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.UseOSConditionAttributeInsteadOfRuntimeCheckRuleId,
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
            if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute, out INamedTypeSymbol? testMethodAttributeSymbol) ||
                !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesRuntimeInformation, out INamedTypeSymbol? runtimeInformationSymbol) ||
                !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingAssert, out INamedTypeSymbol? assertSymbol))
            {
                return;
            }

            IMethodSymbol? isOSPlatformMethod = runtimeInformationSymbol.GetMembers("IsOSPlatform")
                .OfType<IMethodSymbol>()
                .FirstOrDefault(m => m.Parameters.Length == 1);

            if (isOSPlatformMethod is null)
            {
                return;
            }

            context.RegisterOperationBlockStartAction(blockContext =>
            {
                if (blockContext.OwningSymbol is not IMethodSymbol methodSymbol)
                {
                    return;
                }

                // Check if the method is a test method
                bool isTestMethod = methodSymbol.GetAttributes().Any(attr =>
                    attr.AttributeClass is not null &&
                    attr.AttributeClass.Inherits(testMethodAttributeSymbol));

                if (!isTestMethod)
                {
                    return;
                }

                blockContext.RegisterOperationAction(
                    operationContext => AnalyzeIfStatement(operationContext, isOSPlatformMethod, assertSymbol),
                    OperationKind.Conditional);
            });
        });
    }

    private static void AnalyzeIfStatement(OperationAnalysisContext context, IMethodSymbol isOSPlatformMethod, INamedTypeSymbol assertSymbol)
    {
        var conditionalOperation = (IConditionalOperation)context.Operation;

        // Only analyze if statements (not ternary expressions)
        if (conditionalOperation.WhenFalse is not null and not IBlockOperation { Operations.Length: 0 })
        {
            // Has an else branch with content - more complex scenario, skip for now
            return;
        }

        // Check if the condition is a RuntimeInformation.IsOSPlatform call (or negation of it)
        if (!TryGetIsOSPlatformCall(conditionalOperation.Condition, isOSPlatformMethod, out bool isNegated, out string? osPlatform))
        {
            return;
        }

        // Check if the body contains early return or Assert.Inconclusive
        if (!IsEarlyReturnOrAssertInconclusive(conditionalOperation.WhenTrue, assertSymbol))
        {
            return;
        }

        // Report diagnostic
        ImmutableDictionary<string, string?>.Builder properties = ImmutableDictionary.CreateBuilder<string, string?>();
        properties.Add(IsNegatedKey, isNegated.ToString());
        properties.Add(OSPlatformKey, osPlatform);

        context.ReportDiagnostic(conditionalOperation.CreateDiagnostic(
            Rule,
            properties: properties.ToImmutable()));
    }

    private static bool TryGetIsOSPlatformCall(IOperation condition, IMethodSymbol isOSPlatformMethod, out bool isNegated, out string? osPlatform)
    {
        isNegated = false;
        osPlatform = null;

        IOperation actualCondition = condition;

        // Handle negation: !RuntimeInformation.IsOSPlatform(...)
        if (actualCondition is IUnaryOperation { OperatorKind: UnaryOperatorKind.Not } unaryOp)
        {
            isNegated = true;
            actualCondition = unaryOp.Operand;
        }

        // Walk down any conversions
        actualCondition = actualCondition.WalkDownConversion();

        // Check if it's an invocation of RuntimeInformation.IsOSPlatform
        if (actualCondition is not IInvocationOperation invocation ||
            !SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.OriginalDefinition, isOSPlatformMethod))
        {
            return false;
        }

        // Get the OS platform from the argument
        if (invocation.Arguments.Length != 1)
        {
            return false;
        }

        IOperation argumentValue = invocation.Arguments[0].Value.WalkDownConversion();

        // The argument is typically OSPlatform.Windows, OSPlatform.Linux, etc.
        if (argumentValue is IPropertyReferenceOperation propertyRef)
        {
            osPlatform = propertyRef.Property.Name;
            return true;
        }

        // Could also be OSPlatform.Create("...") call
        if (argumentValue is IInvocationOperation createInvocation &&
            createInvocation.TargetMethod.Name == "Create" &&
            createInvocation.Arguments.Length == 1 &&
            createInvocation.Arguments[0].Value.ConstantValue is { HasValue: true, Value: string platformName })
        {
            osPlatform = platformName;
            return true;
        }

        return false;
    }

    private static bool IsEarlyReturnOrAssertInconclusive(IOperation? whenTrue, INamedTypeSymbol assertSymbol)
    {
        if (whenTrue is null)
        {
            return false;
        }

        // If it's a block, check its contents
        if (whenTrue is IBlockOperation blockOperation)
        {
            foreach (IOperation operation in blockOperation.Operations)
            {
                if (IsReturnOrAssertInconclusive(operation, assertSymbol))
                {
                    return true;
                }
            }

            return false;
        }

        // Single statement (not in a block)
        return IsReturnOrAssertInconclusive(whenTrue, assertSymbol);
    }

    private static bool IsReturnOrAssertInconclusive(IOperation operation, INamedTypeSymbol assertSymbol)
    {
        // Check for return statement
        if (operation is IReturnOperation)
        {
            return true;
        }

        // Check for Assert.Inconclusive call
        if (operation is IExpressionStatementOperation { Operation: IInvocationOperation invocation })
        {
            if (SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.ContainingType, assertSymbol) &&
                invocation.TargetMethod.Name == "Inconclusive")
            {
                return true;
            }
        }

        return false;
    }
}
