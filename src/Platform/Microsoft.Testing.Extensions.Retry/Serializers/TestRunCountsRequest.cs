// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Serializers;

namespace Microsoft.Testing.Platform.Extensions.RetryFailedTests.Serializers;

/// <summary>
/// Reports one attempt's outcome tally back to the retry orchestrator at the end of its test session.
/// </summary>
/// <remarks>
/// The orchestrator is the only component that sees every attempt, so it is the only one that can summarize the run
/// as a whole. It therefore needs the full breakdown rather than just a total, because the per-attempt summaries are
/// suppressed in favour of the single retry summary.
/// <para>
/// Both ends of this pipe are always the same build — the orchestrator relaunches its own executable for every
/// attempt — so the payload can be extended without a protocol-compatibility window.
/// </para>
/// </remarks>
internal sealed class TestRunCountsRequest(int passedTests, int failedTests, int skippedTests) : IRequest
{
    public int PassedTests { get; } = passedTests;

    public int FailedTests { get; } = failedTests;

    public int SkippedTests { get; } = skippedTests;

    /// <summary>
    /// Gets the number of tests that actually executed (skipped excluded), which is what the failure-threshold
    /// policy measures its percentage against.
    /// </summary>
    public int ExecutedTests => PassedTests + FailedTests;

    /// <summary>
    /// Gets the total number of tests including skipped ones, matching what the platform run summary calls "total".
    /// </summary>
    public int TotalTests => PassedTests + FailedTests + SkippedTests;
}

internal sealed class TestRunCountsRequestSerializer : NamedPipeSerializer<TestRunCountsRequest>, INamedPipeSerializer
{
    public override int Id => 4;

    protected override TestRunCountsRequest DeserializeCore(Stream stream)
    {
        int passed = ReadInt(stream);
        int failed = ReadInt(stream);
        int skipped = ReadInt(stream);
        return new(passed, failed, skipped);
    }

    protected override void SerializeCore(TestRunCountsRequest objectToSerialize, Stream stream)
    {
        WriteInt(stream, objectToSerialize.PassedTests);
        WriteInt(stream, objectToSerialize.FailedTests);
        WriteInt(stream, objectToSerialize.SkippedTests);
    }
}
