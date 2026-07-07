// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.IPC.Models;

// Sent once by the test host on the reverse "server control" pipe (see HandshakeMessagePropertyNames.
// ServerControlPipeName). The test host parks this long-poll request and the SDK completes it with a
// ServerControlMessage whenever it wants to push a control signal (e.g. CancelSession). Introduced with
// protocol version 1.4.0; the request carries no payload.
internal sealed class WaitForServerControlRequest : IRequest
{
    public static readonly WaitForServerControlRequest CachedInstance = new();
}
