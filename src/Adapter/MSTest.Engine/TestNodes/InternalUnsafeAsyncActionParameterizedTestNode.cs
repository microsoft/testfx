// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

namespace Microsoft.Testing.Framework;

/// <summary>
/// WARNING: This type is public, but is meant for use only by MSTest source generator. Unannounced breaking changes to this API may happen.
/// </summary>
/// <typeparam name="TData">Type that holds the parameter data.</typeparam>
public sealed class InternalUnsafeAsyncActionParameterizedTestNode<TData>
    : TestNode, IParameterizedTestNode, IParameterizedAsyncActionTestNode
{
    public required Func<ITestExecutionContext, TData, Task> Body { get; init; }

    public required Func<IEnumerable<TData>> GetArguments { get; init; }

    Func<IEnumerable> IParameterizedTestNode.GetArguments => GetArguments;

    async Task IParameterizedAsyncActionTestNode.InvokeAsync(ITestExecutionContext testExecutionContext, Func<Func<Task>, Task> safeInvoke)
        => await InternalUnsafeParameterizedTestNodeHelper.InvokeAsync(
            GetArguments,
            item => Body(testExecutionContext, item),
            safeInvoke).ConfigureAwait(false);

    TestNode IExpandableTestNode.GetExpandedTestNode(object arguments, string argumentFragmentUid, string argumentFragmentDisplayName)
        => InternalUnsafeParameterizedTestNodeHelper.ExpandAsyncActionNode(this, arguments, argumentFragmentUid, argumentFragmentDisplayName, Body);
}
