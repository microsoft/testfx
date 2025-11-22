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
/// MSTEST0059: <inheritdoc cref="Resources.AvoidBlockingCallsInTestsTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class AvoidBlockingCallsInTestsAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.AvoidBlockingCallsInTestsTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.AvoidBlockingCallsInTestsDescription), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.AvoidBlockingCallsInTestsMessageFormat), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor AvoidBlockingCallsInTestsRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.AvoidBlockingCallsInTestsRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Design,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(AvoidBlockingCallsInTestsRule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            // Get the required symbols
            if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingThread, out INamedTypeSymbol? threadSymbol) ||
                !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTask, out INamedTypeSymbol? taskSymbol) ||
                !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute, out INamedTypeSymbol? testMethodAttributeSymbol) ||
                !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestInitializeAttribute, out INamedTypeSymbol? testInitializeAttributeSymbol) ||
                !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestCleanupAttribute, out INamedTypeSymbol? testCleanupAttributeSymbol) ||
                !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingClassInitializeAttribute, out INamedTypeSymbol? classInitializeAttributeSymbol) ||
                !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingClassCleanupAttribute, out INamedTypeSymbol? classCleanupAttributeSymbol) ||
                !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingAssemblyInitializeAttribute, out INamedTypeSymbol? assemblyInitializeAttributeSymbol) ||
                !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingAssemblyCleanupAttribute, out INamedTypeSymbol? assemblyCleanupAttributeSymbol))
            {
                return;
            }

            context.RegisterOperationAction(
                context => AnalyzeInvocation(context, threadSymbol, taskSymbol, testMethodAttributeSymbol, testInitializeAttributeSymbol, testCleanupAttributeSymbol, classInitializeAttributeSymbol, classCleanupAttributeSymbol, assemblyInitializeAttributeSymbol, assemblyCleanupAttributeSymbol),
                OperationKind.Invocation);
        });
    }

    private static void AnalyzeInvocation(
        OperationAnalysisContext context,
        INamedTypeSymbol threadSymbol,
        INamedTypeSymbol taskSymbol,
        INamedTypeSymbol testMethodAttributeSymbol,
        INamedTypeSymbol testInitializeAttributeSymbol,
        INamedTypeSymbol testCleanupAttributeSymbol,
        INamedTypeSymbol classInitializeAttributeSymbol,
        INamedTypeSymbol classCleanupAttributeSymbol,
        INamedTypeSymbol assemblyInitializeAttributeSymbol,
        INamedTypeSymbol assemblyCleanupAttributeSymbol)
    {
        var invocationOperation = (IInvocationOperation)context.Operation;
        IMethodSymbol method = invocationOperation.TargetMethod;

        // Check if we're inside a test-related method
        if (context.ContainingSymbol is not IMethodSymbol containingMethod)
        {
            return;
        }

        // Check if the containing method is a test method or test fixture method
        if (!IsTestRelatedMethod(containingMethod, testMethodAttributeSymbol, testInitializeAttributeSymbol, testCleanupAttributeSymbol, classInitializeAttributeSymbol, classCleanupAttributeSymbol, assemblyInitializeAttributeSymbol, assemblyCleanupAttributeSymbol))
        {
            return;
        }

        // Check if the invocation is Thread.Sleep
        if (SymbolEqualityComparer.Default.Equals(method.ContainingType, threadSymbol) && method.Name == "Sleep")
        {
            context.ReportDiagnostic(invocationOperation.Syntax.CreateDiagnostic(AvoidBlockingCallsInTestsRule, "Thread.Sleep"));
            return;
        }

        // Check if the invocation is Task.Wait
        if (SymbolEqualityComparer.Default.Equals(method.ContainingType, taskSymbol) && method.Name == "Wait")
        {
            context.ReportDiagnostic(invocationOperation.Syntax.CreateDiagnostic(AvoidBlockingCallsInTestsRule, "Task.Wait"));
            return;
        }
    }

    private static bool IsTestRelatedMethod(
        IMethodSymbol method,
        INamedTypeSymbol testMethodAttributeSymbol,
        INamedTypeSymbol testInitializeAttributeSymbol,
        INamedTypeSymbol testCleanupAttributeSymbol,
        INamedTypeSymbol classInitializeAttributeSymbol,
        INamedTypeSymbol classCleanupAttributeSymbol,
        INamedTypeSymbol assemblyInitializeAttributeSymbol,
        INamedTypeSymbol assemblyCleanupAttributeSymbol)
    {
        ImmutableArray<AttributeData> attributes = method.GetAttributes();
        foreach (AttributeData attribute in attributes)
        {
            if (attribute.AttributeClass is null)
            {
                continue;
            }

            // Check if the method has any test-related attribute
            if (attribute.AttributeClass.Inherits(testMethodAttributeSymbol) ||
                SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, testInitializeAttributeSymbol) ||
                SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, testCleanupAttributeSymbol) ||
                SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, classInitializeAttributeSymbol) ||
                SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, classCleanupAttributeSymbol) ||
                SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, assemblyInitializeAttributeSymbol) ||
                SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, assemblyCleanupAttributeSymbol))
            {
                return true;
            }
        }

        return false;
    }
}
