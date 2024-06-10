// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice;

namespace Microsoft.Testing.Platform.ServerMode;

internal class ServerModePerCallOutputDevice : IPlatformOutputDevice
{
    public string Uid => nameof(ServerModePerCallOutputDevice);

    public string Version => AppVersion.DefaultSemVer;

    public string DisplayName => nameof(ServerModePerCallOutputDevice);

    public string Description => nameof(ServerModePerCallOutputDevice);

    public Task DisplayAfterSessionEndRunAsync() => Task.CompletedTask;

    public Task DisplayAsync(IOutputDeviceDataProducer producer, IOutputDeviceData data) => Task.CompletedTask;

    public Task DisplayBannerAsync(string? bannerMessage) => Task.CompletedTask;

    public Task DisplayBeforeSessionStartAsync() => Task.CompletedTask;

    public Task<bool> IsEnabledAsync() => Task.FromResult(false);
}
