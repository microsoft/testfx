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
/// MSTEST0074: <inheritdoc cref="Resources.UndeclaredProcessGlobalStateMutationTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class UndeclaredProcessGlobalStateMutationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// Diagnostic property carrying the <c>WellKnownResources</c> member name the code fix should use.
    /// </summary>
    internal const string ResourceMemberPropertyKey = ParallelSafetyHelper.ResourceMemberPropertyKey;

    private static readonly LocalizableResourceString Title = new(nameof(Resources.UndeclaredProcessGlobalStateMutationTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.UndeclaredProcessGlobalStateMutationMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.UndeclaredProcessGlobalStateMutationDescription), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.UndeclaredProcessGlobalStateMutationRuleId,
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
            INamedTypeSymbol? consoleSymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemConsole);
            if (environmentSymbol is null && consoleSymbol is null)
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
                context => AnalyzeInvocation(context, environmentSymbol, consoleSymbol, testMethodAttributeSymbol, fixtureAttributeSymbols, doNotParallelizeAttributeSymbol, resourceLockAttributeSymbol),
                OperationKind.Invocation);
        });
    }

    private static void AnalyzeInvocation(
        OperationAnalysisContext context,
        INamedTypeSymbol? environmentSymbol,
        INamedTypeSymbol? consoleSymbol,
        INamedTypeSymbol? testMethodAttributeSymbol,
        ImmutableHashSet<INamedTypeSymbol> fixtureAttributeSymbols,
        INamedTypeSymbol? doNotParallelizeAttributeSymbol,
        INamedTypeSymbol? resourceLockAttributeSymbol)
    {
        var invocation = (IInvocationOperation)context.Operation;
        IMethodSymbol targetMethod = invocation.TargetMethod;
        if (!targetMethod.IsStatic)
        {
            return;
        }

        string? api = null;
        string? resourceKey = null;
        string? resourceMember = null;

        if (environmentSymbol is not null
            && targetMethod.Name == "SetEnvironmentVariable"
            && SymbolEqualityComparer.Default.Equals(targetMethod.ContainingType, environmentSymbol))
        {
            api = "Environment.SetEnvironmentVariable";
            resourceKey = WellKnownResourceKeys.EnvironmentVariables;
            resourceMember = "EnvironmentVariables";
        }
        else if (consoleSymbol is not null
            && targetMethod.Name is "SetOut" or "SetError" or "SetIn"
            && SymbolEqualityComparer.Default.Equals(targetMethod.ContainingType, consoleSymbol))
        {
            api = $"Console.{targetMethod.Name}";
            resourceKey = WellKnownResourceKeys.Console;
            resourceMember = "Console";
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

        if (ParallelSafetyHelper.HasResourceLockFor(testMethod, resourceLockAttributeSymbol, resourceKey!))
        {
            return;
        }

        ImmutableDictionary<string, string?> properties = ImmutableDictionary<string, string?>.Empty
            .Add(ResourceMemberPropertyKey, resourceMember);

        context.ReportDiagnostic(invocation.CreateDiagnostic(Rule, properties, api, resourceMember!));
    }
}
