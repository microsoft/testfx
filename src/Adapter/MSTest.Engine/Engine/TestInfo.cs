// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Framework;

internal sealed class TestInfo(TestNode testNode) : ITestInfo
{
    public TestNodeUid StableUid => TestNode.StableUid;

    public string DisplayName => TestNode.DisplayName;

    public IProperty[] Properties => TestNode.Properties;

    public TestNode TestNode { get; } = testNode;
}
