// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Framework.Helpers;

namespace Microsoft.Testing.Framework;

/// <summary>
/// WARNING: This type is public, but is meant for use only by MSTest source generator. Unannounced breaking changes to this API may happen.
/// </summary>
/// <typeparam name="TData">Type that holds the parameter data.</typeparam>
public sealed class InternalUnsafeActionParameterizedTestNode<TData>
    : TestNode, IParameterizedTestNode, IParameterizedAsyncActionTestNode
{
    public required Action<ITestExecutionContext, TData> Body { get; init; }

    public required Func<IEnumerable<TData>> GetArguments { get; init; }

    Func<IEnumerable> IParameterizedTestNode.GetArguments => GetArguments;

    async Task IParameterizedAsyncActionTestNode.InvokeAsync(ITestExecutionContext testExecutionContext, Func<Func<Task>, Task> safeInvoke)
    {
        foreach (TData item in GetArguments())
        {
            await safeInvoke(() =>
            {
                Body(testExecutionContext, item);
                return Task.CompletedTask;
            }).ConfigureAwait(false);
        }
    }

    TestNode IExpandableTestNode.GetExpandedTestNode(object arguments, string argumentFragmentUid, string argumentFragmentDisplayName)
        => new InternalUnsafeActionTestNode
        {
            StableUid = TestNodeExpansionHelper.GenerateStableUid(StableUid, argumentFragmentUid),
            DisplayName = TestNodeExpansionHelper.GenerateDisplayName(DisplayName, argumentFragmentDisplayName),
            Body = testExecutionContext => Body(testExecutionContext, (TData)arguments),
            Properties = Properties,
        };
}
