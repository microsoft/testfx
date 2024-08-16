// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable CS0618 // Type or member is obsolete

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
    [Obsolete("Client is obsolete, use the Microsoft.Testing.Platform.Services.IClientInfo instead")]
    public ClientInfo Client { get; }
}
