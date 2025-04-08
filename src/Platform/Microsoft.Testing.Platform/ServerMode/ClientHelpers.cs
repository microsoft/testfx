// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.ServerMode;

internal static class ClientHelpers
{
    internal static string? ClientName { get; set; }

    internal static Version? ClientVersion { get; set; }

    private static readonly Version? MinimumVisualStudioClientVersionWithFixedLocationImplementation = new("1.0.1");

    public static bool UseWrongLocationImplementation()
        => ClientName == WellKnownClients.VisualStudio &&
            ClientVersion is not null &&
            ClientVersion < MinimumVisualStudioClientVersionWithFixedLocationImplementation;
}
