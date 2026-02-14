// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

namespace Microsoft.Testing.Framework;

/// <summary>
/// WARNING: This type is public, but is meant for use only by MSTest source generator. Unannounced breaking changes to this API may happen.
/// </summary>
public sealed class InternalUnsafeAsyncActionTestNode : TestNode, IAsyncActionTestNode
{
    public required Func<ITestExecutionContext, Task> Body { get; init; }

    async Task IAsyncActionTestNode.InvokeAsync(ITestExecutionContext testExecutionContext)
        => await Body(testExecutionContext).ConfigureAwait(false);
}
