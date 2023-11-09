// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.ServerMode;

public static class ServerModeManagerExtensions
{
    public static void StartTcpServer(this IServerModeManager serverModeManager, int serverPort)
    {
        ArgumentGuard.IsNotNull(serverModeManager);
        ((ServerModeManager)serverModeManager).CommunicationProtocol = new JsonRpcTcpServer(serverPort);
    }

    public static void ConnectToTcpClient(this IServerModeManager serverModeManager, string clientHostName, int clientPort)
    {
        ArgumentGuard.IsNotNull(serverModeManager);
        ArgumentGuard.IsNotNull(clientHostName);
        ((ServerModeManager)serverModeManager).CommunicationProtocol = new JsonRpcTcpServerToSingleClient(clientHostName, clientPort);
    }
}
