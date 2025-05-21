// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Framework;

internal sealed class MSTestEngineBannerCapability : IBannerMessageOwnerCapability
{
    private readonly IPlatformInformation _platformInformation;

    public MSTestEngineBannerCapability(IPlatformInformation platformInformation) => _platformInformation = platformInformation;

    public Task<string?> GetBannerMessageAsync()
    {
        StringBuilder bannerMessage = new();
        bannerMessage.Append("MSTest.Engine v");
        bannerMessage.Append(MSTestEngineRepositoryVersion.Version);

#if NETCOREAPP
        if (RuntimeFeature.IsDynamicCodeCompiled)
#endif
        {
            bannerMessage.Append(" [");
#if NET6_0_OR_GREATER
            bannerMessage.Append(RuntimeInformation.RuntimeIdentifier);
#else
            bannerMessage.Append(RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant());
#endif
            bannerMessage.Append(" - ");
            bannerMessage.Append(RuntimeInformation.FrameworkDescription);
            bannerMessage.Append(']');
        }

        return Task.FromResult<string?>(bannerMessage.ToString());
    }
}
