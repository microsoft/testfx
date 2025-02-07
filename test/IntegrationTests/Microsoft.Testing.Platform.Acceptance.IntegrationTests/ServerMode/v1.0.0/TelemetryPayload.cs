// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Newtonsoft.Json;

namespace Microsoft.Testing.Platform.ServerMode.IntegrationTests.Messages.V100;

public record TelemetryPayload
(
    [property: JsonProperty(nameof(TelemetryPayload.EventName))]
    string EventName,

    [property: JsonProperty("metrics")]
    IDictionary<string, string> Metrics);
