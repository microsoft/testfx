// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using EasyNamedPipes;

using Microsoft.CodeAnalysis;

namespace Microsoft.Testing.Platform.IPC.Models;

[Embedded]
internal sealed record FileArtifactMessage(
    string? FullPath,
    string? DisplayName,
    string? Description,
    string? TestUid,
    string? TestDisplayName,
    string? SessionUid);

[Embedded]
[PipeSerializableMessage("DotNetTestProtocol", 7)]
internal sealed record FileArtifactMessages(
    [property: PipePropertyId(1)] string? ExecutionId,
    [property: PipePropertyId(2)] string? InstanceId,
    [property: PipePropertyId(3)] FileArtifactMessage[] FileArtifacts) : IRequest;
