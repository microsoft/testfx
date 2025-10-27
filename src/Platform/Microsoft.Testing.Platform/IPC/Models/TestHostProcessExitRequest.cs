// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace Microsoft.Testing.Platform.IPC.Models;

[Embedded]
[GenerateSerializer(TestHostProcessExitRequestFieldsId.MessagesSerializerId)]
internal sealed class TestHostProcessExitRequest(int returnCode) : IRequest
{
    public int ExitCode { get; } = returnCode;
}
