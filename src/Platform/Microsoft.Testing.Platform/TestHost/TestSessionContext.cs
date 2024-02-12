// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.TestHost;

/// <summary>
/// Represents the context of a test session.
/// </summary>
public class TestSessionContext
{
    internal TestSessionContext(SessionUid sessionUid, ClientInfo client)
    {
        SessionUid = sessionUid;
        Client = client;
    }

    /// <summary>
    /// Gets the unique identifier of the test session.
    /// </summary>
    public SessionUid SessionUid { get; }

    /// <summary>
    /// Gets the client information associated with the test session.
    /// </summary>
    public ClientInfo Client { get; }
}
