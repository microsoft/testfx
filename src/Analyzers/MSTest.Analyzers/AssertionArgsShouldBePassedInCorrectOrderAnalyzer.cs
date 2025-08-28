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
/// MSTEST0017: <inheritdoc cref="Resources.AssertionArgsShouldBePassedInCorrectOrderTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class AssertionArgsShouldBePassedInCorrectOrderAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableArray<string> SupportedMethodNames = ImmutableArray.Create([
        "AreEqual",
        "AreNotEqual",
        "AreSame",
        "AreNotSame"
    ]);

    private static readonly LocalizableResourceString Title = new(nameof(Resources.AssertionArgsShouldBePassedInCorrectOrderTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.AssertionArgsShouldBePassedInCorrectOrderDescription), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.AssertionArgsShouldBePassedInCorrectOrderMessageFormat), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.AssertionArgsShouldBePassedInCorrectOrderRuleId,
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
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingAssert, out INamedTypeSymbol? assertSymbol))
            {
                context.RegisterOperationAction(context => AnalyzeOperation(context, assertSymbol), OperationKind.Invocation);
            }
        });
    }

    private static bool IsConstant(IArgumentOperation argumentOperation)
        => argumentOperation.Value.WalkDownConversion().ConstantValue.HasValue;

    private static void AnalyzeOperation(OperationAnalysisContext context, INamedTypeSymbol assertSymbol)
    {
        var invocationOperation = (IInvocationOperation)context.Operation;

        // This is not an invocation of the expected assertion methods.
        if (!SupportedMethodNames.Contains(invocationOperation.TargetMethod.Name)
            || !SymbolEqualityComparer.Default.Equals(assertSymbol, invocationOperation.TargetMethod.ContainingType)
            || FindExpectedAndActualArguments(invocationOperation) is not (IArgumentOperation expectedArgument, IArgumentOperation actualArgument))
        {
            return;
        }

        // If the actual value is a constant or a literal and expected is not, then the arguments are in the wrong order.
        // Note that we don't report if both are literals or constants, as there is no real fix for this.
        // If both are literals or constants, the assert will always pass or always fail.
        if (IsConstant(actualArgument) && !IsConstant(expectedArgument))
        {
            context.ReportDiagnostic(invocationOperation.CreateDiagnostic(Rule));
            return;
        }

        // Only apply name-based heuristics when neither argument is a constant.
        // If one argument is a constant, trust that the developer has placed it correctly.
        bool expectedIsConstant = IsConstant(expectedArgument);
        bool actualIsConstant = IsConstant(actualArgument);
        
        if (!expectedIsConstant && !actualIsConstant)
        {
            if (actualArgument.Value.GetReferencedMemberOrLocalOrParameter() is { } actualSymbol)
            {
                if (actualSymbol.Name.StartsWith("expected", StringComparison.Ordinal)
                    || actualSymbol.Name.StartsWith("_expected", StringComparison.Ordinal)
                    || actualSymbol.Name.StartsWith("Expected", StringComparison.Ordinal))
                {
                    context.ReportDiagnostic(invocationOperation.CreateDiagnostic(Rule));
                    return;
                }
            }

            if (expectedArgument.Value.GetReferencedMemberOrLocalOrParameter() is { } expectedSymbol
                && expectedSymbol.Name.StartsWith("actual", StringComparison.Ordinal))
            {
                context.ReportDiagnostic(invocationOperation.CreateDiagnostic(Rule));
            }
        }
    }

    private static (IArgumentOperation ExpectedArgument, IArgumentOperation ActualArgument)? FindExpectedAndActualArguments(IInvocationOperation invocationOperation)
    {
        if (invocationOperation.Arguments.Length < 2)
        {
            return null;
        }

        if (invocationOperation.Arguments.FirstOrDefault(IsExpectedOrNotExpectedName) is not { } expectedArgument)
        {
            return null;
        }

        IArgumentOperation? actualArgument = invocationOperation.Arguments.FirstOrDefault(x => string.Equals(x.Parameter?.Name, "actual", StringComparison.Ordinal));
        return actualArgument is null
            ? null
            : (expectedArgument, actualArgument);

        // Local functions
        static bool IsExpectedOrNotExpectedName(IArgumentOperation argumentOperation)
            => argumentOperation.Parameter is not null
            && (string.Equals(argumentOperation.Parameter?.Name, "expected", StringComparison.Ordinal)
                || string.Equals(argumentOperation.Parameter?.Name, "notExpected", StringComparison.Ordinal));
    }
}
