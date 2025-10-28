// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using EasyNamedPipes;

using Microsoft.CodeAnalysis;

namespace Microsoft.Testing.Platform.IPC.Models;

[Embedded]
[PipeSerializableMessage("TestHostProtocol", 1)]
internal sealed record TestHostProcessExitRequest(
    [property: PipePropertyId(1)] int? ExitCode)
    : IRequest;
