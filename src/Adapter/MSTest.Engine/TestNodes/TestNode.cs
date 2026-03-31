// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Framework;

[DebuggerDisplay("StableUid = {StableUid.Value}, TestsCount = {Tests.Length}, PropertiesCount = {Properties.Length}")]
public class TestNode
{
    public required TestNodeUid StableUid { get; init; }

    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the name of the edge that connects this node to its parent.
    /// </summary>
    public string? OverriddenEdgeName { get; init; }

    public IProperty[] Properties { get; init; } = [];

    public TestNode[] Tests { get; init; } = [];
}
