// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Serializers;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions.Serializers;

/// <summary>
/// IPC request sent from the test host to the test host controller as soon as the streaming sidecar
/// file is provisioned (lazily, on the first observed test result). The controller stores the path so
/// that, if the test host process crashes before <see cref="ReportFileNameRequest"/> arrives, it can
/// still recover the durably-written test results from the sidecar and produce a partial TRX.
/// </summary>
internal sealed class TrxStreamLocationRequest(string filePath) : IRequest
{
    public string FilePath { get; } = filePath;
}

internal sealed class TrxStreamLocationRequestSerializer : NamedPipeSerializer<TrxStreamLocationRequest>, INamedPipeSerializer
{
    public override int Id => 3;

    protected override TrxStreamLocationRequest DeserializeCore(Stream stream)
        => new(ReadString(stream));

    protected override void SerializeCore(TrxStreamLocationRequest objectToSerialize, Stream stream)
        => WriteString(stream, objectToSerialize.FilePath);
}
