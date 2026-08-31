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
/// MSTEST0079: Use '[ArchitectureCondition]' attribute instead of 'RuntimeInformation.ProcessArchitecture' checks with early return or 'Assert.Inconclusive'.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class UseArchitectureConditionAttributeInsteadOfRuntimeCheckAnalyzer : DiagnosticAnalyzer
{
    internal const string IsNegatedKey = nameof(IsNegatedKey);
    internal const string ArchitectureKey = nameof(ArchitectureKey);

    private static readonly LocalizableResourceString Title = new(nameof(Resources.UseArchitectureConditionAttributeInsteadOfRuntimeCheckTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.UseArchitectureConditionAttributeInsteadOfRuntimeCheckMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.UseArchitectureConditionAttributeInsteadOfRuntimeCheckDescription), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.UseArchitectureConditionAttributeInsteadOfRuntimeCheckRuleId,
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
                !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesRuntimeInformation, out INamedTypeSymbol? runtimeInformationSymbol) ||
                !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesArchitecture, out INamedTypeSymbol? architectureSymbol))
            {
                return;
            }

            // '[ArchitectureCondition]' and 'TestArchitectures' only exist on the .NET flavor of MSTest, so there is
            // nothing to suggest when compiling against the .NET Framework reference assemblies.
            if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingArchitectureConditionAttribute, out INamedTypeSymbol? _) ||
                !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestArchitectures, out INamedTypeSymbol? testArchitecturesSymbol))
            {
                return;
            }

            IPropertySymbol? processArchitectureProperty = runtimeInformationSymbol.GetMembers("ProcessArchitecture")
                .OfType<IPropertySymbol>()
                .FirstOrDefault();

            if (processArchitectureProperty is null)
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
                // 'ArchitectureConditionAttribute' would not compile because it isn't 'AllowMultiple', and adding one
                // next to any other condition attribute can change behavior: conditions are OR-combined when they
                // share a 'GroupName' and AND-combined otherwise, and a custom attribute's 'GroupName' is an arbitrary
                // property implementation that can't be resolved statically.
                if (!attributes.Any(attr => attr.AttributeClass is not null && attr.AttributeClass.Inherits(testMethodAttributeSymbol)) ||
                    attributes.Any(attr => attr.AttributeClass is not null && attr.AttributeClass.Inherits(conditionBaseAttributeSymbol)))
                {
                    return;
                }

                ConditionGuardHelper.MethodBodyHolder methodBody = ConditionGuardHelper.RegisterMethodBodyCapture(blockContext);

                blockContext.RegisterOperationAction(
                    operationContext => AnalyzeIfStatement(operationContext, processArchitectureProperty, architectureSymbol, testArchitecturesSymbol, assertSymbol, methodBody),
                    OperationKind.Conditional);
            });
        });
    }

    private static void AnalyzeIfStatement(
        OperationAnalysisContext context,
        IPropertySymbol processArchitectureProperty,
        INamedTypeSymbol architectureSymbol,
        INamedTypeSymbol testArchitecturesSymbol,
        INamedTypeSymbol assertSymbol,
        ConditionGuardHelper.MethodBodyHolder methodBody)
    {
        var conditionalOperation = (IConditionalOperation)context.Operation;

        if (!ConditionGuardHelper.IsSkipGuard(conditionalOperation, methodBody.Body, assertSymbol))
        {
            return;
        }

        if (!TryGetArchitectureFromCondition(conditionalOperation.Condition, processArchitectureProperty, architectureSymbol, out bool isNegated, out string? architecture))
        {
            return;
        }

        // Only report when the 'Architecture' member has a matching 'TestArchitectures' flag. Newer runtimes can
        // introduce architectures that the referenced MSTest version doesn't know about (for example 'RiscV64' is
        // only present in the net9.0 flavor), and suggesting a value that doesn't compile would be a bad fix.
        if (!testArchitecturesSymbol.GetMembers(architecture).OfType<IFieldSymbol>().Any())
        {
            return;
        }

        ImmutableDictionary<string, string?>.Builder properties = ImmutableDictionary.CreateBuilder<string, string?>();
        properties.Add(IsNegatedKey, isNegated.ToString());
        properties.Add(ArchitectureKey, architecture);

        context.ReportDiagnostic(conditionalOperation.CreateDiagnostic(
            Rule,
            properties: properties.ToImmutable()));
    }

    private static bool TryGetArchitectureFromCondition(
        IOperation condition,
        IPropertySymbol processArchitectureProperty,
        INamedTypeSymbol architectureSymbol,
        out bool isNegated,
        [NotNullWhen(true)] out string? architecture)
    {
        isNegated = false;
        architecture = null;

        if (condition.WalkDownConversion() is not IBinaryOperation { OperatorKind: BinaryOperatorKind.Equals or BinaryOperatorKind.NotEquals } binaryOperation)
        {
            return false;
        }

        IOperation left = binaryOperation.LeftOperand.WalkDownConversion();
        IOperation right = binaryOperation.RightOperand.WalkDownConversion();

        // Support both 'RuntimeInformation.ProcessArchitecture == Architecture.X64' and the flipped form.
        if (IsProcessArchitectureReference(left, processArchitectureProperty))
        {
            architecture = GetArchitectureMemberName(right, architectureSymbol);
        }
        else if (IsProcessArchitectureReference(right, processArchitectureProperty))
        {
            architecture = GetArchitectureMemberName(left, architectureSymbol);
        }

        if (architecture is null)
        {
            return false;
        }

        // '!=' means the test bails out on every other architecture, so the architecture is the required one
        // (include mode). '==' means the test bails out on that architecture only (exclude mode).
        isNegated = binaryOperation.OperatorKind == BinaryOperatorKind.NotEquals;
        return true;
    }

    private static bool IsProcessArchitectureReference(IOperation operation, IPropertySymbol processArchitectureProperty)
        => operation is IPropertyReferenceOperation propertyReference &&
            SymbolEqualityComparer.Default.Equals(propertyReference.Property, processArchitectureProperty);

    private static string? GetArchitectureMemberName(IOperation operation, INamedTypeSymbol architectureSymbol)
        => operation is IFieldReferenceOperation { Field: { IsStatic: true, ContainingType: { } containingType } field } &&
            SymbolEqualityComparer.Default.Equals(containingType, architectureSymbol)
            ? field.Name
            : null;
}
