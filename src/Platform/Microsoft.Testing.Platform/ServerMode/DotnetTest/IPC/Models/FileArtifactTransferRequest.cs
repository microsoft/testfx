// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.IPC.Models;

internal sealed record FileArtifactTransferRequest(
    string? ExecutionId,
    string? InstanceId,
    string? TransferId,
    string? FileName,
    long TotalLength,
    long Offset,
    byte[] Data,
    bool IsFinal) : IRequest;
