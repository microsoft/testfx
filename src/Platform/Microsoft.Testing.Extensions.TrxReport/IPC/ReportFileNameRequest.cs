// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using EasyNamedPipes;

using Microsoft.Testing.Platform.IPC;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions.Serializers;

[PipeSerializableMessage("TrxReportProtocol", 0)]
internal sealed class ReportFileNameRequest(string fileName) : IRequest
{
    [PipePropertyId(1)]
    public string FileName { get; } = fileName;
}
