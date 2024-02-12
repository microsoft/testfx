// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.TestHost;

/// <summary>
/// Represents a unique identifier for a session.
/// </summary>
public readonly struct SessionUid(string value)
{
    /// <summary>
    /// Gets the value of the session identifier.
    /// </summary>
    public string Value { get; } = value;

    /// <summary>
    /// Returns a string representation of the SessionUid.
    /// </summary>
    /// <returns>A string representation of the SessionUid.</returns>
    public override string ToString() => $"SessionUid {{ Value = {Value} }}";
}
