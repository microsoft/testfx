// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

namespace Microsoft.Testing.Framework;

public abstract class InternalUnsafeParameterizedTestNodeBase<TData> : TestNode, IParameterizedAsyncActionTestNode, IExpandableTestNode
{
    async Task IParameterizedAsyncActionTestNode.InvokeAsync(ITestExecutionContext testExecutionContext, Func<Func<Task>, Task> safeInvoke)
        => await InvokeWithArgumentsAsync(CreateInvokeBody(testExecutionContext), safeInvoke).ConfigureAwait(false);

    TestNode IExpandableTestNode.GetExpandedTestNode(object arguments, string argumentFragmentUid, string argumentFragmentDisplayName)
        => Expand(arguments, argumentFragmentUid, argumentFragmentDisplayName);

    internal abstract Func<TData, Task> CreateInvokeBody(ITestExecutionContext testExecutionContext);

    internal abstract Task InvokeWithArgumentsAsync(Func<TData, Task> invokeBodyAsync, Func<Func<Task>, Task> safeInvoke);

    internal abstract TestNode Expand(object arguments, string argumentFragmentUid, string argumentFragmentDisplayName);
}
