// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

namespace Microsoft.Testing.Framework;

/// <summary>
/// Represents a unique identifier for a test node.
/// </summary>
/// <param name="Value">The unique identifier.</param>
[DebuggerDisplay("{Value}")]
public sealed record TestNodeUid(string Value)
{
    /// <summary>
    /// Implicitly converts a string to a <see cref="TestNodeUid"/>.
    /// </summary>
    /// <param name="value">The unique identifier.</param>
    public static implicit operator TestNodeUid(string value)
        => new(value);
}
