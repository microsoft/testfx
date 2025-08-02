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
/// MSTEST0053: <inheritdoc cref="Resources.PreferTestContextWriteTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class PreferTestContextWriteAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.PreferTestContextWriteTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.PreferTestContextWriteMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.PreferTestContextWriteDescription), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.PreferTestContextWriteRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

    // Method names to detect (Write, WriteLine)
    private static readonly ImmutableHashSet<string> TargetMethodNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "Write",
        "WriteLine");

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemConsole, out INamedTypeSymbol? consoleSymbol)
                || !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemDiagnosticsTrace, out INamedTypeSymbol? traceSymbol)
                || !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemDiagnosticsDebug, out INamedTypeSymbol? debugSymbol)
                || !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute, out INamedTypeSymbol? testMethodAttributeSymbol)
                || !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestClassAttribute, out INamedTypeSymbol? testClassAttributeSymbol))
            {
                return;
            }

            context.RegisterOperationAction(context => AnalyzeInvocation(context, consoleSymbol, traceSymbol, debugSymbol, testMethodAttributeSymbol, testClassAttributeSymbol), OperationKind.Invocation);
        });
    }

    private static void AnalyzeInvocation(
        OperationAnalysisContext context,
        INamedTypeSymbol consoleSymbol,
        INamedTypeSymbol traceSymbol,
        INamedTypeSymbol debugSymbol,
        INamedTypeSymbol testMethodAttributeSymbol,
        INamedTypeSymbol testClassAttributeSymbol)
    {
        var invocation = (IInvocationOperation)context.Operation;

        // Check if this is a call to Console.Write*, Trace.Write*, or Debug.Write*
        if (invocation.TargetMethod.ContainingType is null || !TargetMethodNames.Contains(invocation.TargetMethod.Name))
        {
            return;
        }

        INamedTypeSymbol? targetType = null;
        string typeName = string.Empty;

        if (SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.ContainingType, consoleSymbol))
        {
            targetType = consoleSymbol;
            typeName = "Console";
        }
        else if (SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.ContainingType, traceSymbol))
        {
            targetType = traceSymbol;
            typeName = "Trace";
        }
        else if (SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.ContainingType, debugSymbol))
        {
            targetType = debugSymbol;
            typeName = "Debug";
        }

        if (targetType is null)
        {
            return;
        }

        // Check if we're in a test context (test method or test class)
        if (!IsInTestContext(context.ContainingSymbol, testMethodAttributeSymbol, testClassAttributeSymbol))
        {
            return;
        }

        // Report diagnostic
        context.ReportDiagnostic(invocation.CreateDiagnostic(Rule, typeName, invocation.TargetMethod.Name));
    }

    private static bool IsInTestContext(ISymbol? containingSymbol, INamedTypeSymbol testMethodAttributeSymbol, INamedTypeSymbol testClassAttributeSymbol)
    {
        // Check if we're in a test method
        if (containingSymbol is IMethodSymbol method && method.HasAttribute(testMethodAttributeSymbol))
        {
            return true;
        }

        return false;
    }
}