// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using EasyNamedPipes;

using Microsoft.Testing.Platform.IPC;

namespace Microsoft.Testing.Platform.Extensions.RetryFailedTests.Serializers;

[PipeSerializableMessage("RetryFailedTestsProtocol", 3)]
internal sealed class GetListOfFailedTestsResponse(string[] failedTestIds) : IResponse
{
    [property: PipePropertyId(1)]
    public string[] FailedTestIds { get; } = failedTestIds;
}
