// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.Messages;

public class TestNodeBuilder
{
    public required TestNodeUid Uid { get; init; }

    public required string DisplayName { get; init; }

    public PropertyBagBuilder Properties { get; init; } = new();

    public TestNode ToImmutableTestNode(TestNodeStateProperty currentState) =>
        new()
        {
            Uid = Uid, 
            DisplayName = DisplayName, 
            Properties = Properties.ToImmutablePropertyBag(currentState)
        };
}
