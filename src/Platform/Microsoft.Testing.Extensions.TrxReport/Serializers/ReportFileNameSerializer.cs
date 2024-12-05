// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Serializers;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions.Serializers;

internal sealed class ReportFileNameRequest(string fileName) : IRequest
{
    public string FileName { get; } = fileName;
}

internal sealed class ReportFileNameRequestSerializer : BaseSerializer, INamedPipeSerializer
{
    public int Id => 1;

    public object Deserialize(Stream stream)
    {
        string reportFileName = ReadString(stream);
        return new ReportFileNameRequest(reportFileName);
    }

    public void Serialize(object objectToSerialize, Stream stream)
    {
        var reportFileName = (ReportFileNameRequest)objectToSerialize;
        WriteString(stream, reportFileName.FileName);
    }
}
