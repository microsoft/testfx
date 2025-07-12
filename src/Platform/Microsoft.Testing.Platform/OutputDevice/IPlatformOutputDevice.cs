// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.OutputDevice;

namespace Microsoft.Testing.Platform.OutputDevice;

internal interface IPlatformOutputDevice : IExtension
{
    Task DisplayBannerAsync(string? bannerMessage, CancellationToken cancellationToken);

    Task DisplayBeforeSessionStartAsync(CancellationToken cancellationToken);

    Task DisplayAfterSessionEndRunAsync(CancellationToken cancellationToken);

    Task DisplayAsync(IOutputDeviceDataProducer producer, IOutputDeviceData data, CancellationToken cancellationToken);

    Task HandleProcessRoleAsync(TestProcessRole processRole, CancellationToken cancellationToken);
}
