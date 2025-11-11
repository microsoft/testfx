// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using EasyNamedPipes;

namespace Microsoft.Testing.Platform.IPC.Models;

[PipeSerializableMessage(ProtocolConstants.ProtocolName, 71)]
internal sealed record FileArtifactMessage(
    [property: PipePropertyId(1)] string? FullPath,
    [property: PipePropertyId(2)] string? DisplayName,
    [property: PipePropertyId(3)] string? Description,
    [property: PipePropertyId(4)] string? TestUid,
    [property: PipePropertyId(5)] string? TestDisplayName,
    [property: PipePropertyId(6)] string? SessionUid);

[PipeSerializableMessage(ProtocolConstants.ProtocolName, 7)]
internal sealed record FileArtifactMessages(
    [property: PipePropertyId(1)] string? ExecutionId,
    [property: PipePropertyId(2)] string? InstanceId,
    [property: PipePropertyId(3)] FileArtifactMessage[] FileArtifacts)
    : IRequest;
