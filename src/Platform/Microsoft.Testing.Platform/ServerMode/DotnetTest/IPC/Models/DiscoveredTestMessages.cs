// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using EasyNamedPipes;

using Microsoft.CodeAnalysis;
using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.IPC.Models;

[Embedded]
internal sealed record DiscoveredTestMessage(
    string? Uid,
    string? DisplayName,
    string? FilePath,
    int? LineNumber,
    string? Namespace,
    string? TypeName,
    string? MethodName,
    string[]? ParameterTypeFullNames,
    TestMetadataProperty[] Traits);

[Embedded]
[PipeSerializableMessage("DotNetTestProtocol", 5)]
internal sealed record DiscoveredTestMessages(
    [property: PipePropertyId(1)] string? ExecutionId,
    [property: PipePropertyId(2)] string? InstanceId,
    [property: PipePropertyId(3)] DiscoveredTestMessage[] DiscoveredMessages) : IRequest;
