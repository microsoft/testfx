// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.ServerMode;

internal sealed partial class ServerModeManager : IServerModeManager
{
    internal ICommunicationProtocol? CommunicationProtocol { get; set; }

    internal IMessageHandlerFactory Build(IServiceProvider serviceProvider)
    {
        ICommandLineOptions commandLineService = serviceProvider.GetCommandLineOptions();

        int? serverPort;
        serverPort = commandLineService.TryGetOptionArgumentList(PlatformCommandLineProvider.PortOptionKey, out string[]? serverPortArgs)
            ? int.Parse(serverPortArgs[0], CultureInfo.InvariantCulture)
            : null;

        int? clientPort;
        clientPort = commandLineService.TryGetOptionArgumentList(PlatformCommandLineProvider.ClientPortOptionKey, out string[]? clientPortArgs)
            ? int.Parse(clientPortArgs[0], CultureInfo.InvariantCulture)
            : null;

        string? clientHostName;
        clientHostName = commandLineService.TryGetOptionArgumentList(PlatformCommandLineProvider.ClientHostOptionKey, out string[]? clientHostArgs)
            ? clientHostArgs[0]
            : "localhost";

        if (CommunicationProtocol is not null)
        {
            switch (CommunicationProtocol)
            {
                case JsonRpcTcpServer tcpServerCommunicationProtocol:
                    {
                        serverPort ??= tcpServerCommunicationProtocol.Port;

                        break;
                    }

                case JsonRpcTcpServerToSingleClient tcpServerToSingleClientCommunicationProtocol:
                    {
                        clientPort ??= tcpServerToSingleClientCommunicationProtocol.ClientPort;

                        if (RoslynString.IsNullOrEmpty(clientHostName))
                        {
                            clientHostName = tcpServerToSingleClientCommunicationProtocol.ClientHostName;
                        }

                        break;
                    }

                default:
                    {
                        throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, PlatformResources.UnknownCommunicationProtocolErrorMessage, CommunicationProtocol.GetType()));
                    }
            }
        }

        return clientPort is not null && clientHostName is not null
            ? new MessageHandlerFactory(clientHostName, clientPort.Value, serviceProvider.GetOutputDevice())
            : new MessageHandlerFactory(serverPort, serviceProvider.GetOutputDevice());
    }
}
