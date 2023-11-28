// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.ServerMode;

internal sealed class JsonRpcTcpServer(int port) : ICommunicationProtocol
{
    public int Port { get; } = port;

    public string Name => nameof(JsonRpcTcpServer);

    public string Version => AppVersion.DefaultSemVer;

    public string Description => PlatformResources.JsonRpcTcpServerDescription;
}
