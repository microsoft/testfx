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
/// MSTEST0075: <inheritdoc cref="Resources.CurrentDirectoryMutationUnderParallelizationTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class CurrentDirectoryMutationUnderParallelizationAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.CurrentDirectoryMutationUnderParallelizationTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.CurrentDirectoryMutationUnderParallelizationMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.CurrentDirectoryMutationUnderParallelizationDescription), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.CurrentDirectoryMutationUnderParallelizationRuleId,
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
            INamedTypeSymbol? environmentSymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemEnvironment);
            INamedTypeSymbol? directorySymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemIODirectory);
            if (environmentSymbol is null && directorySymbol is null)
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

            ImmutableHashSet<INamedTypeSymbol> classScopedFixtureAttributeSymbols = ParallelSafetyHelper.GetClassScopedFixtureAttributeSymbols(compilation);
            INamedTypeSymbol? resourceLockAttributeSymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingResourceLockAttribute);

            if (directorySymbol is not null)
            {
                context.RegisterOperationAction(
                    context => AnalyzeInvocation(context, directorySymbol, testMethodAttributeSymbol, fixtureAttributeSymbols, classScopedFixtureAttributeSymbols, doNotParallelizeAttributeSymbol, resourceLockAttributeSymbol),
                    OperationKind.Invocation);
            }

            if (environmentSymbol is not null)
            {
                // Both a plain assignment ('Environment.CurrentDirectory = x') and a compound assignment
                // ('Environment.CurrentDirectory += x', which reads then writes) mutate the process-global directory.
                context.RegisterOperationAction(
                    context => AnalyzeAssignment(context, environmentSymbol, testMethodAttributeSymbol, fixtureAttributeSymbols, classScopedFixtureAttributeSymbols, doNotParallelizeAttributeSymbol, resourceLockAttributeSymbol),
                    OperationKind.SimpleAssignment,
                    OperationKind.CompoundAssignment);
            }
        });
    }

    private static void AnalyzeInvocation(
        OperationAnalysisContext context,
        INamedTypeSymbol directorySymbol,
        INamedTypeSymbol? testMethodAttributeSymbol,
        ImmutableHashSet<INamedTypeSymbol> fixtureAttributeSymbols,
        ImmutableHashSet<INamedTypeSymbol> classScopedFixtureAttributeSymbols,
        INamedTypeSymbol? doNotParallelizeAttributeSymbol,
        INamedTypeSymbol? resourceLockAttributeSymbol)
    {
        var invocation = (IInvocationOperation)context.Operation;
        IMethodSymbol targetMethod = invocation.TargetMethod;
        if (!targetMethod.IsStatic
            || targetMethod.Name != "SetCurrentDirectory"
            || !SymbolEqualityComparer.Default.Equals(targetMethod.ContainingType, directorySymbol))
        {
            return;
        }

        Report(context, invocation, "Directory.SetCurrentDirectory", testMethodAttributeSymbol, fixtureAttributeSymbols, classScopedFixtureAttributeSymbols, doNotParallelizeAttributeSymbol, resourceLockAttributeSymbol);
    }

    private static void AnalyzeAssignment(
        OperationAnalysisContext context,
        INamedTypeSymbol environmentSymbol,
        INamedTypeSymbol? testMethodAttributeSymbol,
        ImmutableHashSet<INamedTypeSymbol> fixtureAttributeSymbols,
        ImmutableHashSet<INamedTypeSymbol> classScopedFixtureAttributeSymbols,
        INamedTypeSymbol? doNotParallelizeAttributeSymbol,
        INamedTypeSymbol? resourceLockAttributeSymbol)
    {
        var assignment = (IAssignmentOperation)context.Operation;
        if (assignment.Target is not IPropertyReferenceOperation propertyReference
            || propertyReference.Property.Name != "CurrentDirectory"
            || !SymbolEqualityComparer.Default.Equals(propertyReference.Property.ContainingType, environmentSymbol))
        {
            return;
        }

        Report(context, assignment, "Environment.CurrentDirectory", testMethodAttributeSymbol, fixtureAttributeSymbols, classScopedFixtureAttributeSymbols, doNotParallelizeAttributeSymbol, resourceLockAttributeSymbol);
    }

    private static void Report(
        OperationAnalysisContext context,
        IOperation operation,
        string api,
        INamedTypeSymbol? testMethodAttributeSymbol,
        ImmutableHashSet<INamedTypeSymbol> fixtureAttributeSymbols,
        ImmutableHashSet<INamedTypeSymbol> classScopedFixtureAttributeSymbols,
        INamedTypeSymbol? doNotParallelizeAttributeSymbol,
        INamedTypeSymbol? resourceLockAttributeSymbol)
    {
        IMethodSymbol? testMethod = ParallelSafetyHelper.GetEnclosingTestMethod(context.ContainingSymbol, fixtureAttributeSymbols, testMethodAttributeSymbol);
        if (testMethod is null)
        {
            return;
        }

        // Opting out of parallelization (sequential phase) removes the race entirely, so stay silent.
        if (ParallelSafetyHelper.IsOptedOutOfParallelization(testMethod, doNotParallelizeAttributeSymbol))
        {
            return;
        }

        // A declared current-directory lock means the author has acknowledged and coordinated the mutation, so
        // stay silent - continuing to warn on coordinated code would erode the ruleset's credibility budget. The
        // "coordinated, but consider not mutating the current directory at all" judgement is intentionally left to
        // the sibling parallel-safety-audit skill.
        if (ParallelSafetyHelper.HasResourceLockFor(testMethod, resourceLockAttributeSymbol, WellKnownResourceKeys.CurrentDirectory))
        {
            return;
        }

        // Only offer the code fix where a lock actually takes effect: the test method, or the test class for a
        // class-scoped fixture. Assembly/global fixtures have no effective target, so report without a fix there.
        string? fixScope = ParallelSafetyHelper.GetResourceLockFixScope(testMethod, testMethodAttributeSymbol, classScopedFixtureAttributeSymbols);

        ImmutableDictionary<string, string?> properties = ImmutableDictionary<string, string?>.Empty;
        if (fixScope is not null)
        {
            properties = properties
                .Add(ParallelSafetyHelper.ResourceMemberPropertyKey, "CurrentDirectory")
                .Add(ParallelSafetyHelper.FixScopePropertyKey, fixScope);
        }

        context.ReportDiagnostic(operation.CreateDiagnostic(Rule, properties, api));
    }
}
