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
/// MSTEST0064: <inheritdoc cref="Resources.AvoidThreadSleepAndTaskWaitInTestsTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class AvoidThreadSleepAndTaskWaitInTestsAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.AvoidThreadSleepAndTaskWaitInTestsTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.AvoidThreadSleepAndTaskWaitInTestsMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.AvoidThreadSleepAndTaskWaitInTestsDescription), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.AvoidThreadSleepAndTaskWaitInTestsRuleId,
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
            INamedTypeSymbol? threadSymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingThread);
            INamedTypeSymbol? taskSymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTask);
            INamedTypeSymbol? taskOfTSymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTask1);

            // Collect the set of attribute symbols that mark a method as "test code".
            ImmutableHashSet<INamedTypeSymbol> testRelatedAttributeSymbols = GetTestRelatedAttributeSymbols(compilation);
            INamedTypeSymbol? testMethodAttributeSymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute);

            // If no test attribute is available in this compilation, there is nothing to analyze.
            if (testRelatedAttributeSymbols.IsEmpty && testMethodAttributeSymbol is null)
            {
                return;
            }

            if (threadSymbol is not null || taskSymbol is not null || taskOfTSymbol is not null)
            {
                context.RegisterOperationAction(
                    context => AnalyzeInvocation(context, threadSymbol, taskSymbol, taskOfTSymbol, testRelatedAttributeSymbols, testMethodAttributeSymbol),
                    OperationKind.Invocation);
            }

            if (taskOfTSymbol is not null)
            {
                context.RegisterOperationAction(
                    context => AnalyzePropertyReference(context, taskOfTSymbol, testRelatedAttributeSymbols, testMethodAttributeSymbol),
                    OperationKind.PropertyReference);
            }
        });
    }

    private static void AnalyzeInvocation(
        OperationAnalysisContext context,
        INamedTypeSymbol? threadSymbol,
        INamedTypeSymbol? taskSymbol,
        INamedTypeSymbol? taskOfTSymbol,
        ImmutableHashSet<INamedTypeSymbol> testRelatedAttributeSymbols,
        INamedTypeSymbol? testMethodAttributeSymbol)
    {
        var invocation = (IInvocationOperation)context.Operation;
        IMethodSymbol targetMethod = invocation.TargetMethod;

        string? offendingApi = null;
        if (threadSymbol is not null
            && targetMethod is { Name: "Sleep", IsStatic: true }
            && SymbolEqualityComparer.Default.Equals(targetMethod.ContainingType, threadSymbol))
        {
            offendingApi = "Thread.Sleep";
        }
        else if (targetMethod is { Name: "Wait", IsStatic: false }
            && (SymbolEqualityComparer.Default.Equals(targetMethod.ContainingType, taskSymbol)
                || (taskOfTSymbol is not null && IsConstructedFrom(targetMethod.ContainingType, taskOfTSymbol))))
        {
            offendingApi = "Task.Wait";
        }

        if (offendingApi is null)
        {
            return;
        }

        if (!IsInsideTestCode(context.ContainingSymbol, testRelatedAttributeSymbols, testMethodAttributeSymbol))
        {
            return;
        }

        context.ReportDiagnostic(invocation.CreateDiagnostic(Rule, offendingApi));
    }

    private static void AnalyzePropertyReference(
        OperationAnalysisContext context,
        INamedTypeSymbol taskOfTSymbol,
        ImmutableHashSet<INamedTypeSymbol> testRelatedAttributeSymbols,
        INamedTypeSymbol? testMethodAttributeSymbol)
    {
        var propertyReference = (IPropertyReferenceOperation)context.Operation;
        IPropertySymbol property = propertyReference.Property;

        if (property is not { Name: "Result", IsStatic: false }
            || !IsConstructedFrom(property.ContainingType, taskOfTSymbol))
        {
            return;
        }

        if (!IsInsideTestCode(context.ContainingSymbol, testRelatedAttributeSymbols, testMethodAttributeSymbol))
        {
            return;
        }

        context.ReportDiagnostic(propertyReference.CreateDiagnostic(Rule, "Task<TResult>.Result"));
    }

    private static bool IsConstructedFrom(INamedTypeSymbol? typeSymbol, INamedTypeSymbol genericDefinition)
        => typeSymbol is not null && SymbolEqualityComparer.Default.Equals(typeSymbol.OriginalDefinition, genericDefinition);

    private static bool IsInsideTestCode(
        ISymbol? containingSymbol,
        ImmutableHashSet<INamedTypeSymbol> testRelatedAttributeSymbols,
        INamedTypeSymbol? testMethodAttributeSymbol)
    {
        // Walk up through local functions / lambdas to find the enclosing user-declared method.
        ISymbol? current = containingSymbol;
        while (current is IMethodSymbol method)
        {
            foreach (AttributeData attribute in method.GetAttributes())
            {
                INamedTypeSymbol? attributeClass = attribute.AttributeClass;
                if (attributeClass is null)
                {
                    continue;
                }

                if (testRelatedAttributeSymbols.Contains(attributeClass))
                {
                    return true;
                }

                if (testMethodAttributeSymbol is not null && attributeClass.Inherits(testMethodAttributeSymbol))
                {
                    return true;
                }
            }

            // Continue walking only when the symbol is synthesized from a local function / lambda body.
            if (method.MethodKind is MethodKind.LocalFunction or MethodKind.AnonymousFunction)
            {
                current = method.ContainingSymbol;
                continue;
            }

            return false;
        }

        return false;
    }

    private static ImmutableHashSet<INamedTypeSymbol> GetTestRelatedAttributeSymbols(Compilation compilation)
    {
        ImmutableHashSet<INamedTypeSymbol>.Builder builder = ImmutableHashSet.CreateBuilder<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        AddIfPresent(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestInitializeAttribute);
        AddIfPresent(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestCleanupAttribute);
        AddIfPresent(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingClassInitializeAttribute);
        AddIfPresent(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingClassCleanupAttribute);
        AddIfPresent(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingAssemblyInitializeAttribute);
        AddIfPresent(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingAssemblyCleanupAttribute);
        AddIfPresent(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingGlobalTestInitializeAttribute);
        AddIfPresent(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingGlobalTestCleanupAttribute);
        return builder.ToImmutable();

        void AddIfPresent(string metadataName)
        {
            if (compilation.GetOrCreateTypeByMetadataName(metadataName) is { } symbol)
            {
                builder.Add(symbol);
            }
        }
    }
}
