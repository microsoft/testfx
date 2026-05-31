// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

namespace Microsoft.Testing.Framework;

/// <summary>
/// WARNING: This type is public, but is meant for use only by MSTest source generator. Unannounced breaking changes to this API may happen.
/// </summary>
/// <typeparam name="TData">Type that holds the parameter data.</typeparam>
public sealed class InternalUnsafeActionParameterizedTestNode<TData>
    : InternalUnsafeParameterizedTestNodeBase<TData>, IParameterizedTestNode
{
    public required Action<ITestExecutionContext, TData> Body { get; init; }

    public required Func<IEnumerable<TData>> GetArguments { get; init; }

    Func<IEnumerable> IParameterizedTestNode.GetArguments => GetArguments;

    internal override Func<TData, Task> CreateInvokeBody(ITestExecutionContext testExecutionContext)
        => item =>
        {
            Body(testExecutionContext, item);
            return Task.CompletedTask;
        };

    internal override Task InvokeWithArgumentsAsync(Func<TData, Task> invokeBodyAsync, Func<Func<Task>, Task> safeInvoke)
        => InternalUnsafeParameterizedTestNodeHelper.InvokeAsync(GetArguments, invokeBodyAsync, safeInvoke);

    internal override TestNode Expand(object arguments, string argumentFragmentUid, string argumentFragmentDisplayName)
        => InternalUnsafeParameterizedTestNodeHelper.ExpandActionNode(this, arguments, argumentFragmentUid, argumentFragmentDisplayName, Body);
}
