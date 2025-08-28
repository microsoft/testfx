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

        // If "expected" is already constant, we shouldn't report any diagnostics and we don't care about "actual".
        if (IsConstant(expectedArgument))
        {
            return;
        }

        // If the actual value is a constant or a literal and expected is not, then the arguments are in the wrong order.
        // Note that we don't report if both are literals or constants, as there is no real fix for this.
        // If both are literals or constants, the assert will always pass or always fail.
        if (IsConstant(actualArgument))
        {
            context.ReportDiagnostic(invocationOperation.CreateDiagnostic(Rule));
            return;
        }

        ISymbol? actualSymbol = actualArgument.Value.GetReferencedMemberOrLocalOrParameter();
        ISymbol? expectedSymbol = expectedArgument.Value.GetReferencedMemberOrLocalOrParameter();
        bool actualIsExpected = actualSymbol is not null && NameIsExpected(actualSymbol.Name);
        bool expectedIsExpected = expectedSymbol is not null && NameIsExpected(expectedSymbol.Name);

        // If both arguments have names indicating it's "expected", don't report a diagnostic.
        if (actualIsExpected && !expectedIsExpected)
        {
            context.ReportDiagnostic(invocationOperation.CreateDiagnostic(Rule));
            return;
        }

        bool expectedIsActual = expectedSymbol is not null && NameIsActual(expectedSymbol.Name);
        bool actualIsActual = actualSymbol is not null && NameIsActual(actualSymbol.Name);

        // If both arguments have names indicating it's "actual", don't report a diagnostic.
        if (expectedIsActual && !actualIsActual)
        {
            context.ReportDiagnostic(invocationOperation.CreateDiagnostic(Rule));
            return;
        }

        static bool NameIsExpected(string name)
            => name.StartsWith("expected", StringComparison.Ordinal) ||
                name.StartsWith("_expected", StringComparison.Ordinal) ||
                name.StartsWith("Expected", StringComparison.Ordinal);


        static bool NameIsActual(string name)
            => name.StartsWith("actual", StringComparison.Ordinal);
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
