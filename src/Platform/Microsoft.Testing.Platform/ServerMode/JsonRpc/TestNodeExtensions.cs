// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform;

internal static class TestNodeExtensions
{
    public static TestNode CloneTestNode(this TestNode testNode)
    {
        var clone = new TestNode()
        {
            Uid = testNode.Uid,
            DisplayName = testNode.DisplayName,
        };

        foreach (IProperty property in testNode.Properties)
        {
            clone.Properties.Add(property);
        }

        return clone;
    }
}
