// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0058: <inheritdoc cref="Resources.AvoidAssertsInCatchBlocksTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class AvoidAssertsInCatchBlocksAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.AvoidAssertsInCatchBlocksTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.AvoidAssertsInCatchBlocksMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.AvoidAssertsInCatchBlocksDescription), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.AvoidAssertsInCatchBlocksRuleId,
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
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(context =>
        {
            Compilation compilation = context.Compilation;
            INamedTypeSymbol? assertSymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingAssert);
            INamedTypeSymbol? stringAssertSymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingStringAssert);
            INamedTypeSymbol? collectionAssertSymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingCollectionAssert);

            if (assertSymbol is not null || stringAssertSymbol is not null || collectionAssertSymbol is not null)
            {
                context.RegisterOperationAction(context => AnalyzeOperation(context, assertSymbol, stringAssertSymbol, collectionAssertSymbol), OperationKind.Invocation);
            }
        });
    }

    private static void AnalyzeOperation(
        OperationAnalysisContext context,
        INamedTypeSymbol? assertSymbol,
        INamedTypeSymbol? stringAssertSymbol,
        INamedTypeSymbol? collectionAssertSymbol)
    {
        var operation = (IInvocationOperation)context.Operation;
        INamedTypeSymbol targetType = operation.TargetMethod.ContainingType;
        bool isAssertType =
            targetType.Equals(assertSymbol, SymbolEqualityComparer.Default) ||
            targetType.Equals(stringAssertSymbol, SymbolEqualityComparer.Default) ||
            targetType.Equals(collectionAssertSymbol, SymbolEqualityComparer.Default);

        if (!isAssertType)
        {
            return;
        }

        // Walk up the operation tree to check if we're inside a catch clause
        if (IsInsideCatchClause(operation))
        {
            context.ReportDiagnostic(operation.CreateDiagnostic(Rule));
        }
    }

    private static bool IsInsideCatchClause(IOperation operation)
    {
        IOperation? current = operation;
        while (current is not null)
        {
            if (current is ICatchClauseOperation)
            {
                return true;
            }

            current = current.Parent;
        }

        return false;
    }
}
