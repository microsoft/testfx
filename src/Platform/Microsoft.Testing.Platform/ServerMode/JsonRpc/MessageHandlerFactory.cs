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

        [UnsupportedOSPlatform("browser")]
        public async Task<IMessageHandler> CreateMessageHandlerAsync(CancellationToken cancellationToken)
        {
            await _outputDevice.DisplayAsync(this, new TextOutputDeviceData(string.Format(CultureInfo.InvariantCulture, PlatformResources.ConnectingToClientHost, _host, _port)), cancellationToken).ConfigureAwait(false);

            TcpClient client = new();
            bool shouldDisposeClient = true;

            try
            {
#if NETCOREAPP
                await client.ConnectAsync(host: _host, port: _port, cancellationToken).ConfigureAwait(false);
#else
                await client.ConnectAsync(host: _host, port: _port).WithCancellationAsync(cancellationToken, observeException: true).ConfigureAwait(false);
#endif
                // On NETCOREAPP, the ConnectAsync overload accepting a CancellationToken
                // delegates to SocketAsyncEventArgs, whose ProcessIOCPResult method
                // (SocketAsyncEventArgs.Windows.cs) registers a cancellation callback via
                // CancellationToken.UnsafeRegister. On Windows, the callback calls
                // Interop.Kernel32.CancelIoEx to cancel the pending overlapped I/O;
                // the completed I/O then surfaces as SocketError.OperationAborted.
                //
                // Because the registration happens *after* the OS call returns, there is
                // a TOCTOU race: the connect can complete successfully at the OS level at
                // the same instant the token fires. CancelIoEx finds no pending I/O to
                // cancel, but the socket's internal state is already torn down, leaving
                // it in a disconnected state. ConnectAsync then returns successfully
                // while the socket is no longer usable, causing GetStream() to throw
                // InvalidOperationException ("The operation is not allowed on non-connected sockets").
                cancellationToken.ThrowIfCancellationRequested();
                NetworkStream stream = client.GetStream();
                IMessageHandler messageHandler = new TcpMessageHandler(client, clientToServerStream: stream, serverToClientStream: stream, FormatterUtilities.CreateFormatter());
                shouldDisposeClient = false;
                return messageHandler;
            }
            finally
            {
                if (shouldDisposeClient)
                {
                    client.Dispose();
                }
            }
        }

        public Task<bool> IsEnabledAsync() => Task.FromResult(false);
    }
}
