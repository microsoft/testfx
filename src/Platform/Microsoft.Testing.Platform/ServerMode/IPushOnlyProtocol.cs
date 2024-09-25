// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.ServerMode;

internal interface IPushOnlyProtocol :
#if NETCOREAPP
    IAsyncDisposable,
#endif
    IDisposable
{
    string Name { get; }

    bool IsServerMode { get; }

    Task AfterCommonServiceSetupAsync();

    Task HelpInvokedAsync();

    Task<bool> IsCompatibleProtocolAsync(string testHostType);

    Task<IPushOnlyProtocolConsumer> GetDataConsumerAsync();

    Task OnExitAsync();
}
