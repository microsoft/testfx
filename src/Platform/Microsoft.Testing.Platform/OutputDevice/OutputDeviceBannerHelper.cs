// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.OutputDevice;

internal static class OutputDeviceBannerHelper
{
#pragma warning disable SA1310 // Field names should not contain underscore
    internal const string TESTINGPLATFORM_CONSOLEOUTPUTDEVICE_SKIP_BANNER = nameof(TESTINGPLATFORM_CONSOLEOUTPUTDEVICE_SKIP_BANNER);
#pragma warning restore SA1310 // Field names should not contain underscore

    internal static string BuildBannerText(IPlatformInformation platformInformation, IRuntimeFeature runtimeFeature, string? longArchitecture, string? runtimeFramework)
    {
        StringBuilder stringBuilder = new();
        stringBuilder.Append(platformInformation.Name);

        if (platformInformation.Version is { } version)
        {
            stringBuilder.Append(CultureInfo.InvariantCulture, $" v{version}");
            if (platformInformation.CommitHash is { } commitHash)
            {
                stringBuilder.Append(CultureInfo.InvariantCulture, $"+{(commitHash.Length >= 10 ? commitHash[..10] : commitHash)}");
            }
        }

        if (platformInformation.BuildDate is { } buildDate)
        {
            stringBuilder.Append(CultureInfo.InvariantCulture, $" (UTC {buildDate.UtcDateTime:d})");
        }

        if (runtimeFeature.IsDynamicCodeSupported)
        {
            stringBuilder.Append(" [");
            stringBuilder.Append(longArchitecture);
            stringBuilder.Append(" - ");
            stringBuilder.Append(runtimeFramework);
            stringBuilder.Append(']');
        }

        return stringBuilder.ToString();
    }
}
