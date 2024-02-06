﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

using Newtonsoft.Json;

namespace Microsoft.Testing.Platform.ServerMode.IntegrationTests.Messages.V100;

public sealed record InitializeRequest(
    [property:JsonProperty("processId")]
    int ProcessId,

    [property:JsonProperty("clientInfo")]
    Microsoft.Testing.Platform.ServerMode.IntegrationTests.Messages.V100.ClientInfo ClientInfo,

    [property:JsonProperty("capabilities")]
    MSTest.Acceptance.IntegrationTests.Messages.V100.ClientCapabilities Capabilities);
