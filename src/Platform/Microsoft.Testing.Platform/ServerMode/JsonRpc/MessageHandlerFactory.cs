// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Sockets;

using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.ServerMode;

internal sealed partial class ServerModeManager
{
    internal sealed class MessageHandlerFactory : IMessageHandlerFactory, IOutputDeviceDataProducer
    {
        private readonly string _host;
        private readonly int _port;
        private readonly IOutputDevice _outputDevice;

        public MessageHandlerFactory(
            string host,
            int port,
            IOutputDevice outputDevice)
        {
            // Workaround for slow "localhost" resolve: https://github.com/dotnet/runtime/issues/31085
            // this will pass 127.0.0.1.
            _host = host != "localhost" ? host : IPAddress.Loopback.ToString();
            _port = port;
            _outputDevice = outputDevice;
        }

        public string Uid => nameof(MessageHandlerFactory);

        public string Version => AppVersion.DefaultSemVer;

        public string DisplayName => nameof(MessageHandlerFactory);

        public string Description => nameof(MessageHandlerFactory);

#pragma warning disable CA1416 // Validate platform compatibility
        public async Task<IMessageHandler> CreateMessageHandlerAsync(CancellationToken cancellationToken)
        {
            await _outputDevice.DisplayAsync(this, new TextOutputDeviceData(string.Format(CultureInfo.InvariantCulture, PlatformResources.ConnectingToClientHost, _host, _port)), cancellationToken).ConfigureAwait(false);

            TcpClient client = new();

#if NETCOREAPP
            await client.ConnectAsync(host: _host, port: _port, cancellationToken).ConfigureAwait(false);
#else
            await client.ConnectAsync(host: _host, port: _port).WithCancellationAsync(cancellationToken, observeException: true).ConfigureAwait(false);
#endif
            NetworkStream stream = client.GetStream();
            return new TcpMessageHandler(client, clientToServerStream: stream, serverToClientStream: stream, FormatterUtilities.CreateFormatter());
        }
#pragma warning restore CA1416

        public Task<bool> IsEnabledAsync() => Task.FromResult(false);
    }
}
