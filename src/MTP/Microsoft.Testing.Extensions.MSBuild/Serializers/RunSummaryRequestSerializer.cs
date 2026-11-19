// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Serializers;

namespace Microsoft.Testing.Extensions.MSBuild.Serializers;

internal sealed record RunSummaryInfoRequest(
    int Total,
    int TotalFailed,
    int TotalPassed,
    int TotalSkipped,
    string? Duration,
    bool AllowSkipped) : IRequest;

internal sealed class RunSummaryInfoRequestSerializer : NamedPipeSerializer<RunSummaryInfoRequest>, INamedPipeSerializer
{
    public override int Id => 3;

    protected override RunSummaryInfoRequest DeserializeCore(Stream stream)
        => new RunSummaryInfoRequest(
            ReadInt(stream),
            ReadInt(stream),
            ReadInt(stream),
            ReadInt(stream),
            ReadString(stream),
            ReadInt(stream) != 0);

    protected override void SerializeCore(RunSummaryInfoRequest objectToSerialize, Stream stream)
    {
        WriteInt(stream, objectToSerialize.Total);
        WriteInt(stream, objectToSerialize.TotalFailed);
        WriteInt(stream, objectToSerialize.TotalPassed);
        WriteInt(stream, objectToSerialize.TotalSkipped);
        WriteString(stream, objectToSerialize.Duration ?? string.Empty);
        WriteInt(stream, objectToSerialize.AllowSkipped ? 1 : 0);
    }
}
