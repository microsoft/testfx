// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Serializers;

namespace Microsoft.Testing.Platform.Extensions.RetryFailedTests.Serializers;

/// <summary>
/// Reports one failed test back to the retry orchestrator. Carries the display name alongside the uid so the
/// orchestrator can list retried and flaky tests by name without having to parse or re-resolve uids.
/// </summary>
/// <remarks>
/// Both ends of this pipe are always the same build: the orchestrator relaunches its own executable for every
/// attempt. The payload can therefore be extended without a protocol-compatibility window, unlike the
/// <c>dotnet test</c> pipe which talks to a separately-versioned SDK.
/// </remarks>
internal sealed class FailedTestRequest(string uid, string displayName) : IRequest
{
    public string Uid { get; } = uid;

    public string DisplayName { get; } = displayName;
}

internal sealed class FailedTestRequestSerializer : NamedPipeSerializer<FailedTestRequest>, INamedPipeSerializer
{
    public override int Id => 1;

    protected override FailedTestRequest DeserializeCore(Stream stream)
    {
        string uid = ReadString(stream);
        string displayName = ReadString(stream);
        return new(uid, displayName);
    }

    protected override void SerializeCore(FailedTestRequest objectToSerialize, Stream stream)
    {
        WriteString(stream, objectToSerialize.Uid);
        WriteString(stream, objectToSerialize.DisplayName);
    }
}
