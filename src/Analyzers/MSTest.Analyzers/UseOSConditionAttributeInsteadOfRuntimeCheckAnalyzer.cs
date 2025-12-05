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
                !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingAssert, out INamedTypeSymbol? assertSymbol))
            {
                return;
            }

            // Try to get RuntimeInformation.IsOSPlatform method
            IMethodSymbol? isOSPlatformMethod = null;
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesRuntimeInformation, out INamedTypeSymbol? runtimeInformationSymbol))
            {
                isOSPlatformMethod = runtimeInformationSymbol.GetMembers("IsOSPlatform")
                    .OfType<IMethodSymbol>()
                    .FirstOrDefault(m => m.Parameters.Length == 1);
            }

            // Try to get OperatingSystem type for IsWindows, IsLinux, etc.
            context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemOperatingSystem, out INamedTypeSymbol? operatingSystemSymbol);

            // We need at least one of the two types
            if (isOSPlatformMethod is null && operatingSystemSymbol is null)
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

                IBlockOperation? methodBody = null;
                blockContext.RegisterOperationAction(
                    operationContext =>
                    {
                        // Capture the method body block operation
                        if (methodBody is null && operationContext.Operation is IBlockOperation block && block.Parent is IMethodBodyOperation)
                        {
                            methodBody = block;
                        }
                    },
                    OperationKind.Block);

                blockContext.RegisterOperationAction(
                    operationContext => AnalyzeIfStatement(operationContext, isOSPlatformMethod, operatingSystemSymbol, assertSymbol, methodBody),
                    OperationKind.Conditional);
            });
        });
    }

    private static void AnalyzeIfStatement(OperationAnalysisContext context, IMethodSymbol? isOSPlatformMethod, INamedTypeSymbol? operatingSystemSymbol, INamedTypeSymbol assertSymbol, IBlockOperation? methodBody)
    {
        var conditionalOperation = (IConditionalOperation)context.Operation;

        // Only analyze if statements (not ternary expressions)
        if (conditionalOperation.WhenFalse is not null and not IBlockOperation { Operations.Length: 0 })
        {
            // Has an else branch with content - more complex scenario, skip for now
            return;
        }

        // Only flag if statements that appear at the very beginning of the method body
        // This ensures we don't flag if statements that come after other code
        if (methodBody is not null && methodBody.Operations.Length > 0)
        {
            IOperation firstOperation = methodBody.Operations[0];
            if (firstOperation != conditionalOperation)
            {
                return;
            }
        }

        // Check if the condition is a RuntimeInformation.IsOSPlatform call or OperatingSystem.Is* call (or negation of it)
        if (!TryGetOSPlatformFromCondition(conditionalOperation.Condition, isOSPlatformMethod, operatingSystemSymbol, out bool isNegated, out string? osPlatform))
        {
            return;
        }

        // Check if the body contains only early return or Assert.Inconclusive as the first statement
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

    private static bool TryGetOSPlatformFromCondition(IOperation condition, IMethodSymbol? isOSPlatformMethod, INamedTypeSymbol? operatingSystemSymbol, out bool isNegated, out string? osPlatform)
    {
        isNegated = false;
        osPlatform = null;

        IOperation actualCondition = condition;

        // Handle negation: !RuntimeInformation.IsOSPlatform(...) or !OperatingSystem.IsWindows()
        if (actualCondition is IUnaryOperation { OperatorKind: UnaryOperatorKind.Not } unaryOp)
        {
            isNegated = true;
            actualCondition = unaryOp.Operand;
        }

        // Walk down any conversions
        actualCondition = actualCondition.WalkDownConversion();

        if (actualCondition is not IInvocationOperation invocation)
        {
            return false;
        }

        // Check for RuntimeInformation.IsOSPlatform
        if (isOSPlatformMethod is not null &&
            SymbolEqualityComparer.Default.Equals(invocation.TargetMethod, isOSPlatformMethod))
        {
            return TryGetOSPlatformFromIsOSPlatformCall(invocation, out osPlatform);
        }

        // Check for OperatingSystem.Is* methods
        return operatingSystemSymbol is not null &&
            SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.ContainingType, operatingSystemSymbol) &&
            TryGetOSPlatformFromOperatingSystemCall(invocation, out osPlatform);
    }

    private static bool TryGetOSPlatformFromIsOSPlatformCall(IInvocationOperation invocation, out string? osPlatform)
    {
        osPlatform = null;

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

    private static bool TryGetOSPlatformFromOperatingSystemCall(IInvocationOperation invocation, out string? osPlatform)
    {
        osPlatform = null;

        // Map OperatingSystem.Is* methods to platform names
        string methodName = invocation.TargetMethod.Name;
        osPlatform = methodName switch
        {
            "IsWindows" => "Windows",
            "IsLinux" => "Linux",
            "IsMacOS" => "OSX",
            "IsFreeBSD" => "FreeBSD",
            "IsAndroid" => "Android",
            "IsIOS" => "iOS",
            "IsTvOS" => "tvOS",
            "IsWatchOS" => "watchOS",
            "IsBrowser" => "Browser",
            "IsWasi" => "Wasi",
            "IsMacCatalyst" => "MacCatalyst",
            _ => null,
        };

        return osPlatform is not null;
    }

    private static bool IsEarlyReturnOrAssertInconclusive(IOperation? whenTrue, INamedTypeSymbol assertSymbol)
    {
        if (whenTrue is null)
        {
            return false;
        }

        // If it's a block, check only the first operation
        if (whenTrue is IBlockOperation blockOperation)
        {
            if (blockOperation.Operations.Length == 0)
            {
                return false;
            }

            // Only check the first operation - must be return or Assert.Inconclusive
            return IsReturnOrAssertInconclusive(blockOperation.Operations[0], assertSymbol);
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
