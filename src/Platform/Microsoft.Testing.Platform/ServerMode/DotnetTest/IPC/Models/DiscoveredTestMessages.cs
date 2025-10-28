// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using EasyNamedPipes;

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.IPC.Models;

internal sealed record DiscoveredTestMessage(
    [property: PipePropertyId(1)] string? Uid,
    [property: PipePropertyId(2)] string? DisplayName,
    [property: PipePropertyId(3)] string? FilePath,
    [property: PipePropertyId(4)] int? LineNumber,
    [property: PipePropertyId(5)] string? Namespace,
    [property: PipePropertyId(6)] string? TypeName,
    [property: PipePropertyId(7)] string? MethodName,
    [property: PipePropertyId(8)] string[]? ParameterTypeFullNames,
    [property: PipePropertyId(9)] TestMetadataProperty[] Traits);

[PipeSerializableMessage("DotNetTestProtocol", 5)]
internal sealed record DiscoveredTestMessages(
    [property: PipePropertyId(1)] string? ExecutionId,
    [property: PipePropertyId(2)] string? InstanceId,
    [property: PipePropertyId(3)] DiscoveredTestMessage[] DiscoveredMessages)
    : IRequest;
