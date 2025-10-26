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
/// MSTEST0026: <inheritdoc cref="Resources.AssertionArgsShouldAvoidConditionalAccessTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class AssertionArgsShouldAvoidConditionalAccessAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableArray<(string MethodName, int ArgumentCountToCheck)> AssertSupportedMethodNames = ImmutableArray.Create([
        ("IsTrue", 1),
        ("IsFalse", 1),
        ("AreEqual", 2),
        ("AreNotEqual", 2),
        ("AreSame", 2),
        ("AreNotSame", 2)
    ]);

    private static readonly ImmutableArray<(string MethodName, int ArgumentCountToCheck)> CollectionAssertSupportedMethodNames = ImmutableArray.Create([
        ("AreEqual", 2),
        ("AreNotEqual", 2),
        ("AreEquivalent", 2),
        ("AreNotEquivalent", 2),
        // TODO: Is it really bad to have Assert.Contains(myCollection, expression_with_conditional_access)? A codefix seems like may not always yield the correct result for this case.
        // TODO: Maybe we should check one argument only (the collection itself)
        // Same applies to DoesNotContain
        ("Contains", 2),
        ("DoesNotContain", 2),
        ("AllItemsAreNotNull", 1),
        ("AllItemsAreUnique", 1),
        ("IsSubsetOf", 2),
        ("IsNotSubsetOf", 2),
        ("AllItemsAreInstancesOfType", 2)
    ]);

    private static readonly ImmutableArray<(string MethodName, int ArgumentCountToCheck)> StringAssertSupportedMethodNames = ImmutableArray.Create([
        ("Contains", 2),
        ("StartsWith", 2),
        ("EndsWith", 2),
        ("Matches", 2),
        ("DoesNotMatch", 2)
    ]);

    private static readonly LocalizableResourceString Title = new(nameof(Resources.AssertionArgsShouldAvoidConditionalAccessTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.AssertionArgsShouldAvoidConditionalAccessMessageFormat), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.AssertionArgsShouldAvoidConditionalAccessRuleId,
        Title,
        MessageFormat,
        description: null,
        Category.Usage,
        DiagnosticSeverity.Info,
        isEnabledByDefault: false);

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
            // Performance note: This analyzer registers for OperationKind.Invocation THREE times (once for each assert type).
            // Each registration is called for EVERY method invocation. The early exit checks in AnalyzeOperation are critical
            // for performance, ensuring we only analyze actual assert method calls.
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

    private static void AnalyzeOperation(OperationAnalysisContext context, INamedTypeSymbol assertSymbol, ImmutableArray<(string MethodName, int ArgumentCountToCheck)> supportedMethodNames)
    {
        var invocationOperation = (IInvocationOperation)context.Operation;

        // Performance: Early exit for non-matching invocations. This check filters out the vast majority of method calls.
        // This is not an invocation of the expected assertion methods.
        (_, int argumentCountToCheck) = supportedMethodNames.FirstOrDefault(x => x.MethodName == invocationOperation.TargetMethod.Name);
        if (argumentCountToCheck == 0
            || !SymbolEqualityComparer.Default.Equals(assertSymbol, invocationOperation.TargetMethod.ContainingType)
            || !HasAnyConditionalAccessOperationChild(invocationOperation, argumentCountToCheck, out Location? additionalLocation))
        {
            return;
        }

        context.ReportDiagnostic(invocationOperation.CreateDiagnostic(Rule, additionalLocations: ImmutableArray.Create(additionalLocation), properties: null));
    }

    private static bool HasAnyConditionalAccessOperationChild(IInvocationOperation invocationOperation, int argumentCountToCheck, [NotNullWhen(true)] out Location? additionalLocation)
    {
        foreach (IArgumentOperation argument in invocationOperation.Arguments)
        {
            if (argument.Parameter is null || argument.Parameter.Ordinal >= argumentCountToCheck)
            {
                continue;
            }

            // Check for conversion operations with conditional access => (s?.Length).
            IOperation value = argument.Value.WalkDownConversion();

            // Check for conditional access
            //      a?.b
            //      a?.b?.c
            //      a.b?.c
            if (value.Kind == OperationKind.ConditionalAccess)
            {
                additionalLocation = value.Syntax.GetLocation();
                return true;
            }

            // Check for binary operations with conditional access => s?.Length > 1.
            if (value is IBinaryOperation binaryOperation)
            {
                if (binaryOperation.LeftOperand.Kind == OperationKind.ConditionalAccess)
                {
                    additionalLocation = binaryOperation.LeftOperand.Syntax.GetLocation();
                    return true;
                }
                else if (binaryOperation.RightOperand.Kind == OperationKind.ConditionalAccess)
                {
                    additionalLocation = binaryOperation.RightOperand.Syntax.GetLocation();
                    return true;
                }
            }
        }

        additionalLocation = null;
        return false;
    }
}
