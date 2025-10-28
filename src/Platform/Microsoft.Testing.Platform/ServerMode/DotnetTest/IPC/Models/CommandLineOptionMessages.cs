// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using EasyNamedPipes;

using Microsoft.CodeAnalysis;

namespace Microsoft.Testing.Platform.IPC.Models;

internal sealed record CommandLineOptionMessage(
    string? Name,
    string? Description,
    bool? IsHidden,
    bool? IsBuiltIn);

[Embedded]
[PipeSerializableMessage("DotNetTestProtocol", 3)]
internal sealed record CommandLineOptionMessages(
    [property: PipePropertyId(1)] string? ModulePath,
    [property: PipePropertyId(2)] CommandLineOptionMessage[]? CommandLineOptionMessageList) : IRequest;
