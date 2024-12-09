// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
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
        private readonly string? _host;
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

        public Task<IMessageHandler> CreateMessageHandlerAsync(CancellationToken cancellationToken)
            => _host is not null
                ? ConnectToTestPlatformClientAsync(_host, _port, cancellationToken)
                : StartTestPlatformServerAsync(port: _port, cancellationToken);

        private async Task<IMessageHandler> ConnectToTestPlatformClientAsync(string clientHost, int clientPort, CancellationToken cancellationToken)
        {
            await _outputDevice.DisplayAsync(this, new TextOutputDeviceData(string.Format(CultureInfo.InvariantCulture, PlatformResources.ConnectingToClientHost, clientHost, clientPort)));

            TcpClient client = new();

#if NETCOREAPP
            await client.ConnectAsync(host: clientHost, port: clientPort, cancellationToken);
#else
            await client.ConnectAsync(host: clientHost, port: clientPort).WithCancellationAsync(cancellationToken, observeException: true);
#endif
            NetworkStream stream = client.GetStream();
            return new TcpMessageHandler(client, clientToServerStream: stream, serverToClientStream: stream, FormatterUtilities.CreateFormatter());
        }

        private async Task<IMessageHandler> StartTestPlatformServerAsync(int? port, CancellationToken cancellationToken)
        {
            port ??= 0;
            IPEndPoint endPoint = new(IPAddress.Loopback, port.Value);
            TcpListener listener = new(endPoint);

            listener.Start();
            try
            {
                await _outputDevice.DisplayAsync(this, new TextOutputDeviceData(string.Format(CultureInfo.InvariantCulture, PlatformResources.StartingServer, ((IPEndPoint)listener.LocalEndpoint).Port)));

#if NETCOREAPP
                TcpClient client = await listener.AcceptTcpClientAsync(cancellationToken);
#else
                TcpClient client = await listener.AcceptTcpClientAsync().WithCancellationAsync(cancellationToken);
#endif
                NetworkStream stream = client.GetStream();
                return new TcpMessageHandler(client, clientToServerStream: stream, serverToClientStream: stream, FormatterUtilities.CreateFormatter());
            }
            catch (OperationCanceledException oc) when (oc.CancellationToken == cancellationToken)
            {
                listener.Stop();
                throw;
            }
        }

        public Task<bool> IsEnabledAsync() => Task.FromResult(false);
    }
}
