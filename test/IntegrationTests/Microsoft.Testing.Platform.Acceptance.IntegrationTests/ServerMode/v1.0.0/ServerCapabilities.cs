// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Newtonsoft.Json;

namespace Microsoft.Testing.Platform.ServerMode.IntegrationTests.Messages.V100;

public sealed record ServerCapabilities(
    [property: JsonProperty("testing")]
    ServerTestingCapabilities Testing);

public sealed record ServerTestingCapabilities(
    [property: JsonProperty("supportsDiscovery")]
    bool SupportsDiscovery,
    [property: JsonProperty("experimental_multiRequestSupport")]
    bool MultiRequestSupport,
    [property: JsonProperty("vsTestProvider")]
    bool VSTestProvider);
