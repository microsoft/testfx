// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class AssertionArgsShouldAvoidConditionalAccessAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableArray<string> AssertSupportedMethodNames = ImmutableArray.Create(new[]
    {
        "IsTrue",
        "IsFalse",
        "AreEqual",
        "AreNotEqual",
        "AreSame",
        "AreNotSame",
    });

    private static readonly ImmutableArray<string> CollectionAssertSupportedMethodNames = ImmutableArray.Create(new[]
    {
        "IsTrue",
        "IsFalse",
        "AreEqual",
        "AreNotEqual",
        "AreEquivalent",
        "AreNotEquivalent",
        "Contains",
        "DoesNotContain",
        "AllItemsAreNotNull",
        "AllItemsAreUnique",
        "IsSubsetOf",
        "IsNotSubsetOf",
        "AllItemsAreInstancesOfType",
    });

    private static readonly ImmutableArray<string> StringAssertSupportedMethodNames = ImmutableArray.Create(new[]
    {
        "Contains",
        "StartsWith",
        "EndsWith",
        "Matches",
        "DoesNotMatch",
    });

    private static readonly LocalizableResourceString Title = new(nameof(Resources.AssertionArgsShouldAvoidConditionalAccessTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.AssertionArgsShouldAvoidConditionalAccessMessageFormat), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.AssertionArgsShouldAvoidConditionalAccessRuleId,
        Title,
        MessageFormat,
        description: null,
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
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingAssert, out INamedTypeSymbol? assertSymbol))
            {
                context.RegisterOperationAction(context => AnalyzeOperation(context, assertSymbol, AssertSupportedMethodNames), OperationKind.Invocation);
            }

            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingCollectionAssert, out INamedTypeSymbol? collectionAssertSymbol))
            {
                context.RegisterOperationAction(context => AnalyzeOperation(context, collectionAssertSymbol, CollectionAssertSupportedMethodNames), OperationKind.Invocation);
            }

            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingStringAssert, out INamedTypeSymbol? stringAssertSymbol))
            {
                context.RegisterOperationAction(context => AnalyzeOperation(context, stringAssertSymbol, StringAssertSupportedMethodNames), OperationKind.Invocation);
            }
        });
    }

    private static void AnalyzeOperation(OperationAnalysisContext context, INamedTypeSymbol assertSymbol, ImmutableArray<string> supportedMethodNames)
    {
        var invocationOperation = (IInvocationOperation)context.Operation;

        // This is not an invocation of the expected assertion methods.
        if (!supportedMethodNames.Contains(invocationOperation.TargetMethod.Name)
            || !SymbolEqualityComparer.Default.Equals(assertSymbol, invocationOperation.TargetMethod.ContainingType)
            || !HasAnyConditionalAccessOperationChild(invocationOperation))
        {
            return;
        }

        context.ReportDiagnostic(invocationOperation.CreateDiagnostic(Rule));
    }

    private static bool HasAnyConditionalAccessOperationChild(IInvocationOperation invocationOperation)
    {
        foreach (IArgumentOperation argument in invocationOperation.Arguments)
        {
            // Check for conditional access
            //      a?.b
            //      a?.b?.c
            //      a.b?.c
            if (argument.Value is IConditionalAccessOperation { Kind: OperationKind.ConditionalAccess })
            {
                return true;
            }

            // Check for binary operations with conditional access => s?.Length > 1.
            if (argument.Value is IBinaryOperation binaryOperation)
            {
                if (binaryOperation.LeftOperand.Kind == OperationKind.ConditionalAccess || binaryOperation.RightOperand.Kind == OperationKind.ConditionalAccess)
                {
                    return true;
                }
            }

            // Check for conversion operations with conditional access => (s?.Length).
            if (argument.Value is IConversionOperation { Operand.Kind: OperationKind.ConditionalAccess })
            {
                return true;
            }
        }

        return false;
    }
}
