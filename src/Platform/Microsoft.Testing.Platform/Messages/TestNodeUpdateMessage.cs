// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.Extensions.Messages;

public sealed class TestNodeUpdateMessage(SessionUid sessionUid, TestNode testNode, TestNodeUid? parentTestNodeUid = null)
    : DataWithSessionUid(nameof(TestNodeUpdateMessage), "This data is used to report a TestNode state change.", sessionUid)
{
    public TestNode TestNode { get; } = testNode;

    public TestNodeUid? ParentTestNodeUid { get; } = parentTestNodeUid;
}
