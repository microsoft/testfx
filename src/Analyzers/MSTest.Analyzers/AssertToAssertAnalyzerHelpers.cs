// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace MSTest.Analyzers;

/// <summary>
/// Shared helpers for analyzers that migrate legacy MSTest assert types to <c>Assert</c>.
/// </summary>
internal static class AssertToAssertAnalyzerHelpers
{
    /// <summary>
    /// Key to retrieve the proper assert method name from the diagnostic properties bag.
    /// </summary>
    internal const string ProperAssertMethodNameKey = nameof(ProperAssertMethodNameKey);

    /// <summary>
    /// Registers <paramref name="analyzeInvocation"/> for <see cref="OperationKind.Invocation"/> operations
    /// whose target method's containing type is <paramref name="sourceAssertTypeMetadataName"/>.
    /// </summary>
    /// <remarks>
    /// The caller is responsible for invoking <see cref="AnalysisContext.ConfigureGeneratedCodeAnalysis"/>
    /// and <see cref="AnalysisContext.EnableConcurrentExecution"/> directly inside its own
    /// <see cref="DiagnosticAnalyzer.Initialize"/> override to satisfy RS1025 and RS1026.
    /// </remarks>
    internal static void RegisterCompilationStart(
        AnalysisContext context,
        string sourceAssertTypeMetadataName,
        Action<OperationAnalysisContext, INamedTypeSymbol> analyzeInvocation)
        => context.RegisterCompilationStartAction(compilationStartContext =>
        {
            if (!compilationStartContext.Compilation.TryGetOrCreateTypeByMetadataName(sourceAssertTypeMetadataName, out INamedTypeSymbol? sourceAssertTypeSymbol))
            {
                return;
            }

            compilationStartContext.RegisterOperationAction(
                operationContext => analyzeInvocation(operationContext, sourceAssertTypeSymbol),
                OperationKind.Invocation);
        });

    /// <summary>
    /// Extracts the invocation operation and its target method when the invocation belongs to <paramref name="sourceAssertTypeSymbol"/>.
    /// </summary>
    /// <returns><see langword="true"/> when the invocation belongs to the source assert type; otherwise <see langword="false"/>.</returns>
    internal static bool TryGetTargetMethod(
        OperationAnalysisContext context,
        INamedTypeSymbol sourceAssertTypeSymbol,
        out IInvocationOperation operation,
        out IMethodSymbol targetMethod)
    {
        operation = (IInvocationOperation)context.Operation;
        targetMethod = operation.TargetMethod;
        return SymbolEqualityComparer.Default.Equals(targetMethod.ContainingType, sourceAssertTypeSymbol);
    }
}
