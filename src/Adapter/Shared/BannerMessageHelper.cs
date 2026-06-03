// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Shared;

[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "Banner capability uses MTP services")]
internal static class BannerMessageHelper
{
    public static string BuildBannerMessage(IPlatformInformation platformInformation, string productName, string version)
    {
        StringBuilder bannerMessage = new();
        bannerMessage.Append(productName);
        bannerMessage.Append(" v");
        bannerMessage.Append(version);

        if (platformInformation.BuildDate is { } buildDate)
        {
            bannerMessage.Append(" (UTC ");
            bannerMessage.Append(buildDate.UtcDateTime.ToShortDateString());
            bannerMessage.Append(')');
        }

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

        return bannerMessage.ToString();
    }
}
