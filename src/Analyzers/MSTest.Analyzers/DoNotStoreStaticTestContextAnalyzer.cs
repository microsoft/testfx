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
/// MSTEST0024: <inheritdoc cref="Resources.DoNotStoreStaticTestContextAnalyzerTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class DoNotStoreStaticTestContextAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.DoNotStoreStaticTestContextAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.DoNotStoreStaticTestContextAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.DoNotStoreStaticTestContextAnalyzerDescription), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.DoNotStoreStaticTestContextAnalyzerRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Usage,
        DiagnosticSeverity.Warning,
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
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestContext, out INamedTypeSymbol? testContextSymbol))
            {
                // Note that compound assignments (for example '+=') are intentionally not handled: they store the
                // result of the underlying operator, not the TestContext parameter itself.
                context.RegisterOperationAction(
                    context => AnalyzeOperation(context, testContextSymbol),
                    OperationKind.SimpleAssignment,
                    OperationKind.CoalesceAssignment,
                    OperationKind.DeconstructionAssignment);
            }
        });
    }

    private static void AnalyzeOperation(OperationAnalysisContext context, INamedTypeSymbol testContextSymbol)
    {
        var assignmentOperation = (IAssignmentOperation)context.Operation;

        if (IsStoringTestContextParameterInStaticMember(assignmentOperation.Target, assignmentOperation.Value, testContextSymbol))
        {
            context.ReportDiagnostic(assignmentOperation.CreateDiagnostic(Rule));
        }
    }

    private static bool IsStoringTestContextParameterInStaticMember(IOperation target, IOperation value, INamedTypeSymbol testContextSymbol)
    {
        // Deconstruction assignments, for example '(s_testContext, _) = (tc, 0);', pair the elements of both tuples.
        if (target.WalkDownConversion() is ITupleOperation targetTuple
            && value.WalkDownConversion() is ITupleOperation valueTuple
            && targetTuple.Elements.Length == valueTuple.Elements.Length)
        {
            for (int i = 0; i < targetTuple.Elements.Length; i++)
            {
                if (IsStoringTestContextParameterInStaticMember(targetTuple.Elements[i], valueTuple.Elements[i], testContextSymbol))
                {
                    return true;
                }
            }

            return false;
        }

        return target is IMemberReferenceOperation { Instance: null }
            && value is IParameterReferenceOperation parameterReferenceOperation
            && SymbolEqualityComparer.Default.Equals(parameterReferenceOperation.Type, testContextSymbol);
    }
}
