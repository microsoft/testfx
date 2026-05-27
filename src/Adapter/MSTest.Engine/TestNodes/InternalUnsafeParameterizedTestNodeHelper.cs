// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Framework.Helpers;

namespace Microsoft.Testing.Framework;

internal static class InternalUnsafeParameterizedTestNodeHelper
{
    public static Task InvokeAsync<TData>(
        Func<IEnumerable<TData>> getArguments,
        Func<TData, Task> invokeBodyAsync,
        Func<Func<Task>, Task> safeInvoke)
        => InvokeAsync(getArguments(), invokeBodyAsync, safeInvoke);

    public static async Task InvokeAsync<TData>(
        Func<Task<IEnumerable<TData>>> getArgumentsAsync,
        Func<TData, Task> invokeBodyAsync,
        Func<Func<Task>, Task> safeInvoke)
        => await InvokeAsync(await getArgumentsAsync().ConfigureAwait(false), invokeBodyAsync, safeInvoke).ConfigureAwait(false);

    public static async Task InvokeAsync<TData>(
        IEnumerable<TData> arguments,
        Func<TData, Task> invokeBodyAsync,
        Func<Func<Task>, Task> safeInvoke)
    {
        foreach (TData item in arguments)
        {
            await safeInvoke(() => invokeBodyAsync(item)).ConfigureAwait(false);
        }
    }

    public static InternalUnsafeActionTestNode ExpandActionNode<TData>(
        TestNode testNode,
        object arguments,
        string argumentFragmentUid,
        string argumentFragmentDisplayName,
        Action<ITestExecutionContext, TData> body)
        => new()
        {
            StableUid = TestNodeExpansionHelper.GenerateStableUid(testNode.StableUid, argumentFragmentUid),
            DisplayName = TestNodeExpansionHelper.GenerateDisplayName(testNode.DisplayName, argumentFragmentDisplayName),
            Body = testExecutionContext => body(testExecutionContext, (TData)arguments),
            Properties = testNode.Properties,
        };

    public static InternalUnsafeAsyncActionTestNode ExpandAsyncActionNode<TData>(
        TestNode testNode,
        object arguments,
        string argumentFragmentUid,
        string argumentFragmentDisplayName,
        Func<ITestExecutionContext, TData, Task> body)
        => new()
        {
            StableUid = TestNodeExpansionHelper.GenerateStableUid(testNode.StableUid, argumentFragmentUid),
            DisplayName = TestNodeExpansionHelper.GenerateDisplayName(testNode.DisplayName, argumentFragmentDisplayName),
            Body = testExecutionContext => body(testExecutionContext, (TData)arguments),
            Properties = testNode.Properties,
        };
}
