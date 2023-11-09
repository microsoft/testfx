// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.Requests;

public sealed class TestNodeUidListFilter : ITestExecutionFilter
{
    public TestNodeUidListFilter(TestNodeUid[] testNodeUids)
    {
        TestNodeUids = testNodeUids;
    }

    public TestNodeUid[] TestNodeUids { get; }
}
