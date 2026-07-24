// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Resources;

/// <summary>
/// Minimal stand-in for the platform's resx-generated <c>PlatformResources</c>. The client links the
/// server's <see cref="Logging.ServerLogMessage"/> verbatim (it is part of the <c>LogEventArgs</c> wire
/// record), and that type reads two <b>display-only</b> labels from <c>PlatformResources</c>. Those labels
/// are never surfaced by the client, so we provide fixed strings here instead of pulling in the whole
/// localized string table + <c>ResourceManager</c>. This keeps the source-only client dependency-free and
/// trim/AOT friendly. These are not wire values, so there is no protocol-drift risk.
/// </summary>
internal static class PlatformResources
{
    public static string ServerLogMessageDisplayName => "Server log message";

    public static string ServerLogMessageDescription => "This data represents a server log message";
}
