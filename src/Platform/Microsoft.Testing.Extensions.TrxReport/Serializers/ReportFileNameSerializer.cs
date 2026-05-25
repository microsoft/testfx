// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Serializers;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions.Serializers;

internal sealed class ReportFileNameRequest(string fileName) : IRequest
{
    public string FileName { get; } = fileName;
}

internal sealed class ReportFileNameRequestSerializer : NamedPipeSerializer<ReportFileNameRequest>, INamedPipeSerializer
{
    public override int Id => 1;

    protected override ReportFileNameRequest DeserializeCore(Stream stream)
        => new(ReadString(stream));

    protected override void SerializeCore(ReportFileNameRequest objectToSerialize, Stream stream)
        => WriteString(stream, objectToSerialize.FileName);
}
