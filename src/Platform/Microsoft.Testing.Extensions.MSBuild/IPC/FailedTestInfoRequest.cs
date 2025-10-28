// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using EasyNamedPipes;

using Microsoft.Testing.Platform.IPC;

namespace Microsoft.Testing.Extensions.MSBuild.Serializers;

[PipeSerializableMessage("MSBuildProtocol", 2)]
internal sealed record FailedTestInfoRequest(
    [property: PipePropertyId(1)]
    string DisplayName,
    [property: PipePropertyId(2)]
    bool IsCanceled,
    [property: PipePropertyId(3)]
    string? Duration,
    [property: PipePropertyId(4)]
    string? ErrorMessage,
    [property: PipePropertyId(5)]
    string? ErrorStackTrace,
    [property: PipePropertyId(6)]
    string? Expected,
    [property: PipePropertyId(7)]
    string? Actual,
    [property: PipePropertyId(8)]
    string? CodeFilePath,
    [property: PipePropertyId(9)]
    int LineNumber) : IRequest;
