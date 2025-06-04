﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using Microsoft.Testing.Platform.Capabilities.TestFramework;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "We can use MTP from this folder")]
internal sealed class MSTestBannerCapability : IBannerMessageOwnerCapability
{
    public Task<string?> GetBannerMessageAsync()
    {
        StringBuilder bannerMessage = new();
        bannerMessage.Append("MSTest v");
        bannerMessage.Append(MSTestVersion.SemanticVersion);

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
#endif
