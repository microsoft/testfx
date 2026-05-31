// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

namespace Microsoft.Testing.Framework;

/// <summary>
/// WARNING: This type is public, but is meant for use only by MSTest source generator. Unannounced breaking changes to this API may happen.
/// </summary>
/// <typeparam name="TData">Type that holds the parameter data.</typeparam>
public abstract class InternalUnsafeParameterizedTestNodeBase<TData> : TestNode, IParameterizedAsyncActionTestNode, IExpandableTestNode
{
    async Task IParameterizedAsyncActionTestNode.InvokeAsync(ITestExecutionContext testExecutionContext, Func<Func<Task>, Task> safeInvoke)
        => await InvokeWithArgumentsAsync(CreateInvokeBody(testExecutionContext), safeInvoke).ConfigureAwait(false);

    TestNode IExpandableTestNode.GetExpandedTestNode(object arguments, string argumentFragmentUid, string argumentFragmentDisplayName)
        => Expand(arguments, argumentFragmentUid, argumentFragmentDisplayName);

    /// <summary>
    /// Creates the delegate that executes the test body for one argument item.
    /// </summary>
    /// <param name="testExecutionContext">Current test execution context.</param>
    /// <returns>Delegate that executes the test body.</returns>
    internal abstract Func<TData, Task> CreateInvokeBody(ITestExecutionContext testExecutionContext);

    /// <summary>
    /// Invokes the provided body delegate for all arguments produced by the node.
    /// </summary>
    /// <param name="invokeBodyAsync">Delegate that executes the test body for one argument item.</param>
    /// <param name="safeInvoke">Wrapper that safely executes each body invocation.</param>
    /// <returns>A task that completes when all arguments have been invoked.</returns>
    internal abstract Task InvokeWithArgumentsAsync(Func<TData, Task> invokeBodyAsync, Func<Func<Task>, Task> safeInvoke);

    /// <summary>
    /// Expands the current parameterized node into a concrete test node for one argument item.
    /// </summary>
    /// <param name="arguments">Argument item used for the expansion.</param>
    /// <param name="argumentFragmentUid">UID fragment for the argument item.</param>
    /// <param name="argumentFragmentDisplayName">Display name fragment for the argument item.</param>
    /// <returns>The expanded test node.</returns>
    internal abstract TestNode Expand(object arguments, string argumentFragmentUid, string argumentFragmentDisplayName);
}
