// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.OutputDevice;

namespace Microsoft.Testing.Platform.OutputDevice;

internal sealed class CustomOutputDeviceAdapter : IPlatformOutputDevice
{
    private readonly ICustomOutputDevice _customOutputDevice;

    public CustomOutputDeviceAdapter(ICustomOutputDevice customOutputDevice)
        => _customOutputDevice = customOutputDevice ?? throw new ArgumentNullException(nameof(customOutputDevice));

    public string Uid => _customOutputDevice.Uid;

    public string Version => _customOutputDevice.Version;

    public string DisplayName => _customOutputDevice.DisplayName;

    public string Description => _customOutputDevice.Description;

    public Task<bool> IsEnabledAsync() => _customOutputDevice.IsEnabledAsync();

    public Task DisplayBannerAsync(string? bannerMessage, CancellationToken cancellationToken)
        => _customOutputDevice.DisplayBannerAsync(bannerMessage, cancellationToken);

    public Task DisplayBeforeSessionStartAsync(CancellationToken cancellationToken)
        => _customOutputDevice.DisplayBeforeSessionStartAsync(cancellationToken);

    public Task DisplayAfterSessionEndRunAsync(CancellationToken cancellationToken)
        => _customOutputDevice.DisplayAfterSessionEndRunAsync(cancellationToken);

    public Task DisplayAsync(IOutputDeviceDataProducer producer, IOutputDeviceData data, CancellationToken cancellationToken)
        => _customOutputDevice.DisplayAsync(producer, data, cancellationToken);

    public Task HandleProcessRoleAsync(TestProcessRole processRole, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
