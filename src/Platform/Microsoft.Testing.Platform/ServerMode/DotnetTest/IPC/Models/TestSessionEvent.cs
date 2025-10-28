// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using EasyNamedPipes;

namespace Microsoft.Testing.Platform.IPC.Models;

[PipeSerializableMessage("DotNetTestProtocol", 8)]
internal sealed record TestSessionEvent(
    [property: PipePropertyId(1)] byte? SessionType,
    [property: PipePropertyId(2)] string? SessionUid,
    [property: PipePropertyId(3)] string? ExecutionId)
    : IRequest;
