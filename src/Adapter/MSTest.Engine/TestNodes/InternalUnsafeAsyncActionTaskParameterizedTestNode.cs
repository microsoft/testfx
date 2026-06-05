// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

namespace Microsoft.Testing.Framework;

/// <summary>
/// WARNING: This type is public, but is meant for use only by MSTest source generator. Unannounced breaking changes to this API may happen.
/// </summary>
/// <typeparam name="TData">Type that holds the parameter data.</typeparam>
public sealed class InternalUnsafeAsyncActionTaskParameterizedTestNode<TData>
    : InternalUnsafeParameterizedTestNodeBase<TData>, ITaskParameterizedTestNode
{
    public required Func<ITestExecutionContext, TData, Task> Body { get; init; }

    public required Func<Task<IEnumerable<TData>>> GetArguments { get; init; }

    Func<Task<IEnumerable>> ITaskParameterizedTestNode.GetArguments => async () => await GetArguments().ConfigureAwait(false);

    internal override Task<IEnumerable<TData>> GetArgumentsAsync()
        => GetArguments();

    internal override Func<TData, Task> CreateInvokeBody(ITestExecutionContext testExecutionContext)
        => item => Body(testExecutionContext, item);

    internal override TestNode Expand(object arguments, string argumentFragmentUid, string argumentFragmentDisplayName)
        => InternalUnsafeParameterizedTestNodeHelper.ExpandAsyncActionNode(this, arguments, argumentFragmentUid, argumentFragmentDisplayName, Body);
}
