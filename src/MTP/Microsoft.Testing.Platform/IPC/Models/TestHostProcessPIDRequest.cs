// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace Microsoft.Testing.Platform.IPC.Models;

[Embedded]
internal sealed class TestHostProcessPIDRequest(int pid) : IRequest
{
    public int PID { get; } = pid;
}
