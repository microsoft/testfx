// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using EasyNamedPipes;

using Microsoft.Testing.Platform.IPC;

namespace Microsoft.Testing.Extensions.HangDump.Serializers;

[PipeSerializableMessage("HangDumpProtocol", 1)]
internal sealed class ActivityIndicatorMutexNameRequest(string mutexName) : IRequest
{
    [PipePropertyId(1)]
    public string MutexName { get; } = mutexName;
}
