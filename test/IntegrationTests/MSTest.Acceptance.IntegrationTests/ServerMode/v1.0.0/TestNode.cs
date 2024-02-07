// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Newtonsoft.Json;

namespace Microsoft.Testing.Platform.ServerMode.IntegrationTests.Messages.V100;

public sealed record TestNodeUpdate
(
    [property: JsonProperty("node")]
    TestNode Node,

    [property: JsonProperty("parent")]
    string ParentUid);

// TODO: complete the object model
public sealed record TestNode
(
    [property: JsonProperty("uid")]
    string Uid,

    [property: JsonProperty("display-name")]
    string DisplayName,

    [property: JsonProperty("node-type")]
    string NodeType,

    [property: JsonProperty("execution-state")]
    string ExecutionState);
