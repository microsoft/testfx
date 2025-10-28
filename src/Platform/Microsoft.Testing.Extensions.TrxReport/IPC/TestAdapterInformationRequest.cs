// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using EasyNamedPipes;

using Microsoft.Testing.Platform.IPC;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions.Serializers;

[PipeSerializableMessage("TrxReportProtocol", 1)]
internal sealed class TestAdapterInformationRequest : IRequest
{
    public TestAdapterInformationRequest(string testAdapterId, string testAdapterVersion)
    {
        TestAdapterId = testAdapterId;
        TestAdapterVersion = testAdapterVersion;
    }

    [PipePropertyId(1)]
    public string TestAdapterId { get; }

    [PipePropertyId(2)]
    public string TestAdapterVersion { get; }
}
