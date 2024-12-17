// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Serializers;

namespace Microsoft.Testing.Platform.Extensions.RetryFailedTests.Serializers;

internal sealed class TotalTestsRunRequest(int totalTests) : IRequest
{
    public int TotalTests { get; } = totalTests;
}

internal sealed class TotalTestsRunRequestSerializer : BaseSerializer, INamedPipeSerializer
{
    public int Id => 4;

    public object Deserialize(Stream stream)
    {
        int totalTestRun = ReadInt(stream);
        return new TotalTestsRunRequest(totalTestRun);
    }

    public void Serialize(object obj, Stream stream)
    {
        var totalTestsRunRequest = (TotalTestsRunRequest)obj;
        WriteInt(stream, totalTestsRunRequest.TotalTests);
    }
}
