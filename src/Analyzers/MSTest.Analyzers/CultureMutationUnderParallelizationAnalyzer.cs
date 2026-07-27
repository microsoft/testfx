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

    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
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
            INamedTypeSymbol? threadSymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingThread);
            if (cultureInfoSymbol is null && threadSymbol is null)
            {
                return;
            }

            INamedTypeSymbol? parallelizeAttributeSymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingParallelizeAttribute);
            if (!ParallelSafetyHelper.IsParallelizationInEffect(compilation, context.Options, parallelizeAttributeSymbol))
            {
                return;
            }

            INamedTypeSymbol? testMethodAttributeSymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute);
            ImmutableHashSet<INamedTypeSymbol> fixtureAttributeSymbols = ParallelSafetyHelper.GetFixtureAttributeSymbols(compilation);
            if (testMethodAttributeSymbol is null && fixtureAttributeSymbols.IsEmpty)
            {
                return;
            }

            INamedTypeSymbol? doNotParallelizeAttributeSymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingDoNotParallelizeAttribute);
            INamedTypeSymbol? resourceLockAttributeSymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingResourceLockAttribute);

            context.RegisterOperationAction(
                context => AnalyzeAssignment(context, cultureInfoSymbol, threadSymbol, testMethodAttributeSymbol, fixtureAttributeSymbols, doNotParallelizeAttributeSymbol, resourceLockAttributeSymbol),
                OperationKind.SimpleAssignment);
        });
    }

    private static void AnalyzeAssignment(
        OperationAnalysisContext context,
        INamedTypeSymbol? cultureInfoSymbol,
        INamedTypeSymbol? threadSymbol,
        INamedTypeSymbol? testMethodAttributeSymbol,
        ImmutableHashSet<INamedTypeSymbol> fixtureAttributeSymbols,
        INamedTypeSymbol? doNotParallelizeAttributeSymbol,
        INamedTypeSymbol? resourceLockAttributeSymbol)
    {
        var assignment = (ISimpleAssignmentOperation)context.Operation;
        if (assignment.Target is not IPropertyReferenceOperation propertyReference)
        {
            return;
        }

        IPropertySymbol property = propertyReference.Property;
        INamedTypeSymbol containingType = property.ContainingType;

        string? api = null;

        // Process-wide: CultureInfo.DefaultThreadCurrentCulture / DefaultThreadCurrentUICulture setters.
        if (cultureInfoSymbol is not null
            && property.Name is "DefaultThreadCurrentCulture" or "DefaultThreadCurrentUICulture"
            && SymbolEqualityComparer.Default.Equals(containingType, cultureInfoSymbol))
        {
            api = $"CultureInfo.{property.Name}";
        }

        // Pooled-thread leak: Thread.CurrentThread.CurrentCulture / CurrentUICulture setters. The thread is
        // returned to the pool and reused by a later test, which then observes the mutated culture.
        else if (threadSymbol is not null
            && property.Name is "CurrentCulture" or "CurrentUICulture"
            && SymbolEqualityComparer.Default.Equals(containingType, threadSymbol))
        {
            api = $"Thread.CurrentThread.{property.Name}";
        }

        if (api is null)
        {
            return;
        }

        IMethodSymbol? testMethod = ParallelSafetyHelper.GetEnclosingTestMethod(context.ContainingSymbol, fixtureAttributeSymbols, testMethodAttributeSymbol);
        if (testMethod is null)
        {
            return;
        }

        if (ParallelSafetyHelper.IsOptedOutOfParallelization(testMethod, doNotParallelizeAttributeSymbol))
        {
            return;
        }

        // No well-known culture resource key exists, so treat any declared [ResourceLock] as the author having
        // coordinated culture access and stay silent rather than guessing which custom key maps to culture.
        if (ParallelSafetyHelper.HasAnyResourceLock(testMethod, resourceLockAttributeSymbol))
        {
            return;
        }

        context.ReportDiagnostic(assignment.CreateDiagnostic(Rule, api));
    }
}
