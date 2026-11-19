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

internal sealed class TestAdapterInformationRequestSerializer : NamedPipeSerializer<TestAdapterInformationRequest>, INamedPipeSerializer
{
    public override int Id => 2;

    protected override TestAdapterInformationRequest DeserializeCore(Stream stream)
        => new(ReadString(stream), ReadString(stream));

    protected override void SerializeCore(TestAdapterInformationRequest objectToSerialize, Stream stream)
    {
        WriteString(stream, objectToSerialize.TestAdapterId);
        WriteString(stream, objectToSerialize.TestAdapterVersion);
    }
}
