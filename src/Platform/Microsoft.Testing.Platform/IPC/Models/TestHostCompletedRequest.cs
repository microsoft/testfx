// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.IPC.Models;

[Embedded]
internal sealed class TestHostCompletedRequest(int returnCode, int unfilteredReturnCode, SessionFileArtifact[] summaryArtifacts) : IRequest
{
    public TestHostCompletedRequest(int returnCode, int unfilteredReturnCode)
        : this(returnCode, unfilteredReturnCode, [])
    {
    }

    public TestHostCompletedRequest(int returnCode)
        : this(returnCode, returnCode)
    {
    }

    public int ExitCode { get; } = returnCode;

    public int UnfilteredExitCode { get; } = unfilteredReturnCode;

    public SessionFileArtifact[] SummaryArtifacts { get; } = summaryArtifacts;
}
