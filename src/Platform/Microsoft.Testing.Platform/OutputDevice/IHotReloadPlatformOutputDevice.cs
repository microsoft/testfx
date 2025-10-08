// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.OutputDevice;

internal interface IHotReloadPlatformOutputDevice : IPlatformOutputDevice
{
    Task DisplayBeforeHotReloadSessionStartAsync(CancellationToken cancellationToken);

    Task DisplayAfterHotReloadSessionEndAsync(CancellationToken cancellationToken);
}
