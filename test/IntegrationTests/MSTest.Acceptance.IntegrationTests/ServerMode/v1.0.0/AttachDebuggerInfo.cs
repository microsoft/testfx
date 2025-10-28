// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Newtonsoft.Json;

namespace MSTest.Acceptance.IntegrationTests.Messages.V100;

public sealed record AttachDebuggerInfo(
    [property:JsonProperty("processId")]
    int ProcessId);
