// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Shared;

namespace Microsoft.Testing.Framework;

internal sealed class MSTestEngineBannerCapability : IBannerMessageOwnerCapability
{
    private readonly IPlatformInformation _platformInformation;

    public MSTestEngineBannerCapability(IPlatformInformation platformInformation) => _platformInformation = platformInformation;

    public Task<string?> GetBannerMessageAsync()
        => Task.FromResult<string?>(BannerMessageHelper.BuildBannerMessage(_platformInformation, "MSTest.Engine", MSTestEngineRepositoryVersion.Version));
}
