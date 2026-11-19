// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Telemetry;

internal interface IPlatformOpenTelemetryServiceWithTestExecutionActivities : IPlatformOpenTelemetryService
{
    IPlatformTestExecutionActivity? StartTestExecutionActivity(
        string name,
        IEnumerable<KeyValuePair<string, object?>>? tags,
        string? parentId,
        DateTimeOffset startTime);
}
