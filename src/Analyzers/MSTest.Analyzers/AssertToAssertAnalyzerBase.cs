// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace MSTest.Analyzers;

/// <summary>
/// Base analyzer for migrations from legacy MSTest assert types to <c>Assert</c>.
/// </summary>
public abstract class AssertToAssertAnalyzerBase : DiagnosticAnalyzer
{
    /// <summary>
    /// Key to retrieve the proper assert method name from the properties bag.
    /// </summary>
    internal const string ProperAssertMethodNameKey = nameof(ProperAssertMethodNameKey);

    /// <inheritdoc />
    public sealed override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(DiagnosticRule);

    /// <inheritdoc />
    public sealed override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (!context.Compilation.TryGetOrCreateTypeByMetadataName(SourceAssertTypeMetadataName, out INamedTypeSymbol? sourceAssertTypeSymbol))
            {
                return;
            }

            context.RegisterOperationAction(context => AnalyzeInvocationOperation(context, sourceAssertTypeSymbol), OperationKind.Invocation);
        });
    }

    /// <summary>
    /// Gets the diagnostic rule exposed by the concrete analyzer.
    /// </summary>
    protected abstract DiagnosticDescriptor DiagnosticRule { get; }

    /// <summary>
    /// Gets the metadata name of the legacy assert type analyzed by this analyzer.
    /// </summary>
    protected abstract string SourceAssertTypeMetadataName { get; }

    /// <summary>
    /// Analyzes invocation operations that target the source assert type.
    /// </summary>
    protected abstract void AnalyzeInvocationOperation(OperationAnalysisContext context, INamedTypeSymbol sourceAssertTypeSymbol);

    /// <summary>
    /// Tries to extract the target invocation method when the invocation belongs to <paramref name="sourceAssertTypeSymbol"/>.
    /// </summary>
    /// <returns><see langword="true"/> when the invocation belongs to the source assert type; otherwise <see langword="false"/>.</returns>
    protected static bool TryGetTargetMethod(
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
