// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Serializers;

namespace Microsoft.Testing.Platform.Extensions.RetryFailedTests.Serializers;

/// <summary>
/// Reports one attempt's outcome tally back to the retry orchestrator at the end of its test session, together with
/// the tests it was asked to retry that actually recovered.
/// </summary>
/// <remarks>
/// The orchestrator is the only component that sees every attempt, so it is the only one that can summarize the run
/// as a whole. It therefore needs the full breakdown rather than just a total, because the per-attempt summaries are
/// suppressed in favour of the single retry summary.
/// <para>
/// The counts are per <em>result</em>, matching what the platform run summary reports, so that a folded data-driven
/// test (several results sharing one test node uid) contributes one unit per data row exactly as it does there.
/// </para>
/// <para>
/// <see cref="RecoveredTestUids"/> is reported explicitly rather than inferred by the orchestrator as "was retried
/// and is absent from the failed set". That inference silently treats a test that never ran — because the attempt
/// crashed, was cancelled, or matched no filter — as having recovered. Only the attempt itself knows which retried
/// tests genuinely passed. The list is bounded by the retry set, so it stays small even for a large suite.
/// </para>
/// <para>
/// Both ends of this pipe are always the same build — the orchestrator relaunches its own executable for every
/// attempt — so the payload can be extended without a protocol-compatibility window.
/// </para>
/// </remarks>
internal sealed class TestRunCountsRequest(int passedTests, int failedTests, int skippedTests, string[] recoveredTestUids, KeyValuePair<string, int>[] skippedRetriedTestResults) : IRequest
{
    public int PassedTests { get; } = passedTests;

    public int FailedTests { get; } = failedTests;

    public int SkippedTests { get; } = skippedTests;

    /// <summary>
    /// Gets the uids of the tests this attempt was asked to retry and which passed. Empty for the first attempt,
    /// which is not retrying anything.
    /// </summary>
    public string[] RecoveredTestUids { get; } = recoveredTestUids;

    /// <summary>
    /// Gets, per test this attempt was asked to retry, how many of its results were skipped. A count rather than a
    /// flag because a folded data-driven test reports one result per data row under a single uid; the orchestrator
    /// adds these to a per-result skipped total, so the units have to match. Such results stop being retried (only
    /// failures are) without ever passing, so the run's skipped count has to absorb them — otherwise the derived
    /// succeeded count would claim they passed.
    /// </summary>
    public KeyValuePair<string, int>[] SkippedRetriedTestResults { get; } = skippedRetriedTestResults;

    /// <summary>
    /// Gets the number of test results that actually executed (skipped excluded), which is the denominator the
    /// failure-threshold policy measures its percentage against.
    /// </summary>
    public int ExecutedTests => PassedTests + FailedTests;

    /// <summary>
    /// Gets the total number of test results including skipped ones, matching what the platform run summary calls
    /// "total".
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
        string[] recovered = ReadUids(stream);

        int skippedRetriedCount = ReadInt(stream);
        var skippedRetried = new KeyValuePair<string, int>[skippedRetriedCount];
        for (int i = 0; i < skippedRetriedCount; i++)
        {
            string uid = ReadString(stream);
            skippedRetried[i] = new(uid, ReadInt(stream));
        }

        return new(passed, failed, skipped, recovered, skippedRetried);
    }

    protected override void SerializeCore(TestRunCountsRequest objectToSerialize, Stream stream)
    {
        WriteInt(stream, objectToSerialize.PassedTests);
        WriteInt(stream, objectToSerialize.FailedTests);
        WriteInt(stream, objectToSerialize.SkippedTests);
        WriteUids(stream, objectToSerialize.RecoveredTestUids);

        WriteInt(stream, objectToSerialize.SkippedRetriedTestResults.Length);
        foreach (KeyValuePair<string, int> entry in objectToSerialize.SkippedRetriedTestResults)
        {
            WriteString(stream, entry.Key);
            WriteInt(stream, entry.Value);
        }
    }

    private static string[] ReadUids(Stream stream)
    {
        int count = ReadInt(stream);
        string[] uids = new string[count];
        for (int i = 0; i < count; i++)
        {
            uids[i] = ReadString(stream);
        }

        return uids;
    }

    private static void WriteUids(Stream stream, string[] uids)
    {
        WriteInt(stream, uids.Length);
        foreach (string uid in uids)
        {
            WriteString(stream, uid);
        }
    }
}
