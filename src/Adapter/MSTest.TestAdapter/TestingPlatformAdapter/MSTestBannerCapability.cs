// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Shared;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "We can use MTP from this folder")]
internal sealed class MSTestBannerCapability : IBannerMessageOwnerCapability
{
    private readonly IPlatformInformation _platformInformation;

    public MSTestBannerCapability(IPlatformInformation platformInformation) => _platformInformation = platformInformation;

    public Task<string?> GetBannerMessageAsync()
        => Task.FromResult<string?>(BannerMessageHelper.BuildBannerMessage(_platformInformation, "MSTest", MSTestVersion.SemanticVersion));
}
#endif
