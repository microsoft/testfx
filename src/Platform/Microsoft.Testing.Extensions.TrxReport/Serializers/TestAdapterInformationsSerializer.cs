// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Serializers;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions.Serializers;

internal sealed class TestAdapterInformationRequest : IRequest
{
    public TestAdapterInformationRequest(string testAdapterId, string testAdapterVersion)
    {
        TestAdapterId = testAdapterId;
        TestAdapterVersion = testAdapterVersion;
    }

    public string TestAdapterId { get; }

    public string TestAdapterVersion { get; }
}

internal sealed class TestAdapterInformationRequestSerializer : BaseSerializer, INamedPipeSerializer
{
    public int Id => 2;

    public object Deserialize(Stream stream)
    {
        string testAdapterId = ReadString(stream);
        string testAdapterVersion = ReadString(stream);
        return new TestAdapterInformationRequest(testAdapterId, testAdapterVersion);
    }

    public void Serialize(object objectToSerialize, Stream stream)
    {
        var testAdapterInformationRequest = (TestAdapterInformationRequest)objectToSerialize;
        WriteString(stream, testAdapterInformationRequest.TestAdapterId);
        WriteString(stream, testAdapterInformationRequest.TestAdapterVersion);
    }
}
