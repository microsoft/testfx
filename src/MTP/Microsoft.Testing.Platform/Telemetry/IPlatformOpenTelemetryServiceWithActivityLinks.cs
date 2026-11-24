// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Telemetry;

internal interface IPlatformOpenTelemetryServiceWithActivityLinks : IPlatformOpenTelemetryService
{
    PlatformActivityContext? CaptureCurrentActivityContext();

    IPlatformActivity? StartActivityWithLink(
        string name,
        IEnumerable<KeyValuePair<string, object?>>? tags,
        string? parentId,
        PlatformActivityContext linkContext);
}
