// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Telemetry;

namespace Microsoft.Testing.Platform.Helpers;

internal static class ExtensionHelper
{
    // ToOTelTags is a static extension method on IExtension with no access to the service provider, and it is
    // called from several hosts and invokers. Rather than change its signature (and every call site) just to pass
    // one flag, the resolved option is published here once by the host before any span is created, and only read
    // afterwards. Reads happen on the host and invoker threads, hence volatile.
    private static volatile bool s_emitLegacyOTelAttributes = true;

    internal static bool EmitLegacyOTelAttributes => s_emitLegacyOTelAttributes;

    /// <summary>
    /// Publishes the resolved legacy-attribute option. Called once by the host at start-up, before any activity
    /// exists, so that <see cref="ToOTelTags"/> honors <c>TESTINGPLATFORM_OTEL_EMIT_LEGACY_ATTRIBUTES</c>.
    /// </summary>
    internal static void ConfigureOTelLegacyAttributes(PlatformOpenTelemetryOptions options)
        => s_emitLegacyOTelAttributes = options.EmitLegacyAttributes;

    public static KeyValuePair<string, object?>[] ToOTelTags(this IExtension extension)
        => EmitLegacyOTelAttributes
            ? [
                new(TestingPlatformSemanticConventions.Attributes.TestExtensionUid, extension.Uid),
                new(TestingPlatformSemanticConventions.Attributes.TestExtensionVersion, extension.Version),
                new(TestingPlatformSemanticConventions.Attributes.TestExtensionDisplayName, extension.DisplayName),

                // Legacy names, kept so existing queries and dashboards keep resolving.
                new("Extension.UID", extension.Uid),
                new("Extension.Version", extension.Version),
                new("Extension.DisplayName", extension.DisplayName),
                new("Extension.Description", extension.Description),
            ]
            : [
                new(TestingPlatformSemanticConventions.Attributes.TestExtensionUid, extension.Uid),
                new(TestingPlatformSemanticConventions.Attributes.TestExtensionVersion, extension.Version),
                new(TestingPlatformSemanticConventions.Attributes.TestExtensionDisplayName, extension.DisplayName),
            ];
}
