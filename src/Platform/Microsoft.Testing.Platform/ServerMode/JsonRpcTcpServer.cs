// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.ServerMode;

internal sealed class JsonRpcTcpServer(int port) : ICommunicationProtocol
{
    public int Port { get; } = port;

    public string Name => nameof(JsonRpcTcpServer);

    public string Version => AppVersion.DefaultSemVer;

    public string Description => "JsonRpc server implementation based on the test platform protocol specification.";
}
