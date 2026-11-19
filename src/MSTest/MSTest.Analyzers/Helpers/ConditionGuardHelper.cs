// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace MSTest.Analyzers.Helpers;

/// <summary>
/// Shared logic for the analyzers that suggest replacing an imperative "skip this test" guard at the top of a
/// test method with a declarative <c>ConditionBaseAttribute</c>-derived attribute (MSTEST0079, MSTEST0080,
/// MSTEST0083).
/// The shape they all recognize is an <c>if</c> statement that is the very first statement of the test method
/// body, has no meaningful <c>else</c> branch, and whose body starts with either a <c>return</c> or an
/// <c>Assert.Inconclusive</c> call.
/// </summary>
internal static class ConditionGuardHelper
{
    /// <summary>
    /// Determines whether the conditional operation has the shape of a skip guard that can be replaced by a
    /// condition attribute.
    /// </summary>
    /// <param name="conditionalOperation">The <c>if</c> statement to inspect.</param>
    /// <param name="methodBody">The body of the enclosing method, when it could be captured.</param>
    /// <param name="assertSymbol">The <c>Assert</c> type symbol.</param>
    /// <returns><see langword="true"/> when the guard can be replaced by a condition attribute.</returns>
    public static bool IsSkipGuard(IConditionalOperation conditionalOperation, IBlockOperation? methodBody, INamedTypeSymbol assertSymbol)
        => HasNoDirectives(conditionalOperation)
        && HasNoElseBranch(conditionalOperation)
        && IsFirstStatementOfMethodBody(conditionalOperation, methodBody)
        && IsEarlyReturnOrAssertInconclusive(conditionalOperation.WhenTrue, assertSymbol);

    /// <summary>
    /// Registers an operation action capturing the body block of the analyzed method so that
    /// <see cref="IsSkipGuard"/> can tell whether a guard is the first statement of the method.
    /// </summary>
    /// <param name="context">The operation block start context.</param>
    /// <returns>A holder whose <see cref="MethodBodyHolder.Body"/> is populated once the body block is seen.</returns>
    public static MethodBodyHolder RegisterMethodBodyCapture(OperationBlockStartAnalysisContext context)
    {
        MethodBodyHolder holder = new();

        context.RegisterOperationAction(
            operationContext =>
            {
                if (holder.Body is null && operationContext.Operation is IBlockOperation { Parent: IMethodBodyOperation } block)
                {
                    holder.Body = block;
                }
            },
            OperationKind.Block);

        return holder;
    }

    private static bool HasNoDirectives(IConditionalOperation conditionalOperation)
        // A guard wrapped in (or containing) preprocessor directives can't be lifted into an attribute: removing it
        // would take its leading '#if' with it and orphan the '#endif', and the attribute would apply to every build
        // configuration instead of the conditional one.
        => !conditionalOperation.Syntax.ContainsDirectives;

    private static bool HasNoElseBranch(IConditionalOperation conditionalOperation)
        => conditionalOperation.WhenFalse is null or IBlockOperation { Operations.Length: 0 };

    private static bool IsFirstStatementOfMethodBody(IConditionalOperation conditionalOperation, IBlockOperation? methodBody)
        // Only flag if statements that appear at the very beginning of the method body. This ensures we don't flag
        // if statements that come after other code, where hoisting the check to an attribute would change behavior.
        => methodBody is null or { Operations.Length: 0 } || methodBody.Operations[0] == conditionalOperation;

    private static bool IsEarlyReturnOrAssertInconclusive(IOperation? whenTrue, INamedTypeSymbol assertSymbol)
        => whenTrue switch
        {
            null => false,
            IBlockOperation { Operations.Length: 0 } => false,
            IBlockOperation blockOperation => IsReturnOrAssertInconclusive(blockOperation.Operations[0], assertSymbol),
            _ => IsReturnOrAssertInconclusive(whenTrue, assertSymbol),
        };

    private static bool IsReturnOrAssertInconclusive(IOperation operation, INamedTypeSymbol assertSymbol)
        => operation switch
        {
            // Only a value-less 'return' is a pure skip. A test method that isn't 'async' can return a value, as in
            // 'return CleanupAsync();', and dropping that would delete a call the test still needs to make.
            IReturnOperation { ReturnedValue: null } => true,
            IExpressionStatementOperation { Operation: IInvocationOperation invocation } =>
                invocation.TargetMethod.Name == "Inconclusive" &&
                SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.ContainingType, assertSymbol),
            _ => false,
        };

    /// <summary>
    /// Holds the body block of the analyzed method once the operation walker has reached it.
    /// </summary>
    internal sealed class MethodBodyHolder
    {
        /// <summary>
        /// Gets or sets the body block of the analyzed method, or <see langword="null"/> when it wasn't captured.
        /// </summary>
        public IBlockOperation? Body { get; set; }
    }
}
