// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable CS0618 // Type or member is obsolete

namespace Microsoft.Testing.Platform.TestHost;

/// <summary>
/// Represents the context of a test session.
/// </summary>
public class TestSessionContext
{
    internal TestSessionContext(SessionUid sessionUid)
        => SessionUid = sessionUid;

    /// <summary>
    /// Gets the unique identifier of the test session.
    /// </summary>
    public SessionUid SessionUid { get; }
}
