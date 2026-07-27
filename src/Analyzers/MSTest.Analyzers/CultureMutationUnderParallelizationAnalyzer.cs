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
/// MSTEST0076: <inheritdoc cref="Resources.CultureMutationUnderParallelizationTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class CultureMutationUnderParallelizationAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.CultureMutationUnderParallelizationTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.CultureMutationUnderParallelizationMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.CultureMutationUnderParallelizationDescription), Resources.ResourceManager, typeof(Resources));

    /// <summary>
    /// Gets the diagnostic descriptor reported by this analyzer.
    /// </summary>
    public static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.CultureMutationUnderParallelizationRuleId,
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
            Compilation compilation = context.Compilation;
            INamedTypeSymbol? cultureInfoSymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemGlobalizationCultureInfo);
            if (cultureInfoSymbol is null)
            {
                return;
            }

            INamedTypeSymbol? parallelizeAttributeSymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingParallelizeAttribute);
            INamedTypeSymbol? doNotParallelizeAttributeSymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingDoNotParallelizeAttribute);
            if (!ParallelSafetyHelper.IsParallelizationInEffect(compilation, context.Options, parallelizeAttributeSymbol, doNotParallelizeAttributeSymbol))
            {
                return;
            }

            INamedTypeSymbol? testMethodAttributeSymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute);
            ImmutableHashSet<INamedTypeSymbol> fixtureAttributeSymbols = ParallelSafetyHelper.GetFixtureAttributeSymbols(compilation);
            if (testMethodAttributeSymbol is null && fixtureAttributeSymbols.IsEmpty)
            {
                return;
            }

            INamedTypeSymbol? resourceLockAttributeSymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingResourceLockAttribute);

            // A plain assignment, a compound assignment, and a null-coalescing assignment ('??=') all write the
            // static culture field, so observe all three assignment shapes.
            context.RegisterOperationAction(
                context => AnalyzeAssignment(context, cultureInfoSymbol, testMethodAttributeSymbol, fixtureAttributeSymbols, doNotParallelizeAttributeSymbol, resourceLockAttributeSymbol),
                OperationKind.SimpleAssignment,
                OperationKind.CompoundAssignment,
                OperationKind.CoalesceAssignment);
        });
    }

    private static void AnalyzeAssignment(
        OperationAnalysisContext context,
        INamedTypeSymbol cultureInfoSymbol,
        INamedTypeSymbol? testMethodAttributeSymbol,
        ImmutableHashSet<INamedTypeSymbol> fixtureAttributeSymbols,
        INamedTypeSymbol? doNotParallelizeAttributeSymbol,
        INamedTypeSymbol? resourceLockAttributeSymbol)
    {
        var assignment = (IAssignmentOperation)context.Operation;
        if (assignment.Target is not IPropertyReferenceOperation propertyReference)
        {
            return;
        }

        IPropertySymbol property = propertyReference.Property;

        // Only CultureInfo.DefaultThreadCurrentCulture / DefaultThreadCurrentUICulture set a process-wide (static)
        // field and are therefore unambiguously unsafe under parallelization. The per-thread and ambient forms
        // (Thread.CurrentThread.Current[UI]Culture, CultureInfo.Current[UI]Culture) delegate to an AsyncLocal that
        // flows with ExecutionContext on modern .NET, so they do not corrupt sibling or later-pooled tests and are
        // intentionally NOT flagged - the judgement call about restoring them in a finally belongs to the sibling
        // parallel-safety-audit skill. See the culture determination recorded in the PR description.
        if (property.Name is not ("DefaultThreadCurrentCulture" or "DefaultThreadCurrentUICulture")
            || !SymbolEqualityComparer.Default.Equals(property.ContainingType, cultureInfoSymbol))
        {
            return;
        }

        string api = $"CultureInfo.{property.Name}";

        IMethodSymbol? testMethod = ParallelSafetyHelper.GetEnclosingTestMethod(context.ContainingSymbol, fixtureAttributeSymbols, testMethodAttributeSymbol);
        if (testMethod is null)
        {
            return;
        }

        if (ParallelSafetyHelper.IsOptedOutOfParallelization(testMethod, doNotParallelizeAttributeSymbol, testMethodAttributeSymbol))
        {
            return;
        }

        // No well-known culture resource key exists, so treat any declared [ResourceLock] as the author having
        // coordinated culture access and stay silent rather than guessing which custom key maps to culture.
        if (ParallelSafetyHelper.HasAnyResourceLock(testMethod, resourceLockAttributeSymbol, testMethodAttributeSymbol))
        {
            return;
        }

        context.ReportDiagnostic(assignment.CreateDiagnostic(Rule, api));
    }
}
