﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

using Newtonsoft.Json;

namespace Microsoft.Testing.Platform.ServerMode.IntegrationTests.Messages.V100;

public sealed record ClientInfo(
    [property:JsonProperty("name")]
    string Name,

    [property:JsonProperty("version")]
    string Version = "1.0.0");
