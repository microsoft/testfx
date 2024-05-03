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
public sealed class DoNotStoreStaticTestContextAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.DoNotStoreStaticTestContextAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.DoNotStoreStaticTestContextAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.DoNotStoreStaticTestContextAnalyzerRuleId,
        Title,
        MessageFormat,
        null,
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
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestContext, out INamedTypeSymbol? testContextSymbol))
            {
                context.RegisterOperationAction(context => AnalyzeOperation(context, testContextSymbol), OperationKind.SimpleAssignment);
            }
        });
    }

    private static void AnalyzeOperation(OperationAnalysisContext context, INamedTypeSymbol testContextSymbol)
    {
        var assignmentOperation = (ISimpleAssignmentOperation)context.Operation;

        if (assignmentOperation.Target is IMemberReferenceOperation memberReferenceOperation
            && memberReferenceOperation.Instance is null
            && assignmentOperation.Value is IParameterReferenceOperation parameterReferenceOperation
            && SymbolEqualityComparer.Default.Equals(parameterReferenceOperation.Type, testContextSymbol))
        {
            context.ReportDiagnostic(assignmentOperation.CreateDiagnostic(Rule));
        }
    }
}
