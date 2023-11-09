// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.TestHost;

public class TestSessionContext
{
    internal TestSessionContext(SessionUid sessionUid, ClientInfo client)
    {
        SessionUid = sessionUid;
        Client = client;
    }

    public SessionUid SessionUid { get; }

    public ClientInfo Client { get; }
}
