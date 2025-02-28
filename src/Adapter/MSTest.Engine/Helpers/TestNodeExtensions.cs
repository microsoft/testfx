// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

using PlatformTestNode = Microsoft.Testing.Platform.Extensions.Messages.TestNode;
using PlatformTestNodeUid = Microsoft.Testing.Platform.Extensions.Messages.TestNodeUid;

namespace Microsoft.Testing.Framework.Helpers;

internal static class TestNodeExtensions
{
    public static PlatformTestNode ToPlatformTestNode(this TestNode testNode)
    {
        if (testNode.DisplayName is null)
        {
            throw new ArgumentException("TestNode must have a DisplayNameFragment", nameof(testNode));
        }

        if (testNode.StableUid is null)
        {
            throw new ArgumentException("TestNode must have a StableUid", nameof(testNode));
        }

        var platformTestNode = new PlatformTestNode
        {
            Uid = testNode.StableUid.ToPlatformTestNodeUid(),
            DisplayName = testNode.DisplayName,
        };

        foreach (IProperty property in testNode.Properties)
        {
            platformTestNode.Properties.Add(property);
        }

        return platformTestNode;
    }

    public static PlatformTestNodeUid ToPlatformTestNodeUid(this TestNodeUid testNodeUid)
        => new(testNodeUid.Value);
}
