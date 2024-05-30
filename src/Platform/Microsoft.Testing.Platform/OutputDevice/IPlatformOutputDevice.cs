// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;

namespace Microsoft.Testing.Platform.OutputDevice;

internal interface IPlatformOutputDevice : IExtension, IOutputDevice
{
    Task DisplayBannerAsync(string? bannerMessage);

    Task DisplayBeforeSessionStartAsync();

    Task DisplayAfterSessionEndRunAsync();
}
