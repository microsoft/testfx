// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.ServerMode.Client;

internal static class MtpClientOperatingSystem
{
    public static bool IsBrowser()
#if NETFRAMEWORK
        => false;
#else
        => RuntimeInformation.IsOSPlatform(OSPlatform.Create("BROWSER"));
#endif
}
