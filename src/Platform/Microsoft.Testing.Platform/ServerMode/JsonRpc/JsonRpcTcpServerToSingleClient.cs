// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.ServerMode;

internal sealed class JsonRpcTcpServerToSingleClient(string clientHostName, int clientPort) : ICommunicationProtocol
{
    public string ClientHostName { get; } = clientHostName;

    public int ClientPort { get; } = clientPort;

    public string Name => nameof(JsonRpcTcpServerToSingleClient);

    public string Version => AppVersion.DefaultSemVer;

    public string Description => PlatformResources.JsonRpcTcpServerToSingleClientDescription;
}
