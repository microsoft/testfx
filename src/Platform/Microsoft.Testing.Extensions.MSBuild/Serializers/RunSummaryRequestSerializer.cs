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
    string? Duration) : IRequest;

internal sealed class RunSummaryInfoRequestSerializer : BaseSerializer, INamedPipeSerializer
{
    public int Id => 3;

    public object Deserialize(Stream stream)
        => new RunSummaryInfoRequest(
            ReadInt(stream),
            ReadInt(stream),
            ReadInt(stream),
            ReadInt(stream),
            ReadString(stream));

    public void Serialize(object objectToSerialize, Stream stream)
    {
        var runSummaryInfoRequest = (RunSummaryInfoRequest)objectToSerialize;
        WriteInt(stream, runSummaryInfoRequest.Total);
        WriteInt(stream, runSummaryInfoRequest.TotalFailed);
        WriteInt(stream, runSummaryInfoRequest.TotalPassed);
        WriteInt(stream, runSummaryInfoRequest.TotalSkipped);
        WriteString(stream, runSummaryInfoRequest.Duration ?? string.Empty);
    }
}
