// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Hosts;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.CommandLine;

internal sealed class InformativeCommandLineHost(int returnValue, IServiceProvider serviceProvider) : IHost, IDisposable
{
    private readonly int _returnValue = returnValue;

    private IPushOnlyProtocol? PushOnlyProtocol => serviceProvider.GetService<IPushOnlyProtocol>();

    public Task<int> RunAsync() => Task.FromResult(_returnValue);

    public void Dispose() => PushOnlyProtocol?.Dispose();
}
