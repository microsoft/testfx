// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using EasyNamedPipes;

using Microsoft.Testing.Platform.IPC;

namespace Microsoft.Testing.Extensions.MSBuild.Serializers;

[PipeSerializableMessage("MSBuildProtocol", 3)]
internal sealed record RunSummaryInfoRequest(
    [property: PipePropertyId(1)]
    int Total,
    [property: PipePropertyId(2)]
    int TotalFailed,
    [property: PipePropertyId(3)]
    int TotalPassed,
    [property: PipePropertyId(4)]
    int TotalSkipped,
    [property: PipePropertyId(5)]
    string? Duration) : IRequest;
