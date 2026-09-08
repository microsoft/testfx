// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.RetryFailedTests.Serializers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.IPC.Serializers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.Policy;

[UnsupportedOSPlatform("browser")]
internal sealed class RetryFailedTestsPipeServer : IDisposable
{
    private readonly NamedPipeServer _singleConnectionNamedPipeServer;
    private readonly string[] _failedTests;

    public RetryFailedTestsPipeServer(IServiceProvider serviceProvider, string[] failedTests, ILogger logger)
    {
        PipeNameDescription pipeNameDescription = NamedPipeServer.GetPipeName(Guid.NewGuid().ToString("N"));
        logger.LogTrace($"Retry server pipe name: '{pipeNameDescription.Name}'");
        _singleConnectionNamedPipeServer = new NamedPipeServer(
            pipeNameDescription,
            CallbackAsync,
            serviceProvider.GetEnvironment(),
            serviceProvider.GetLoggerFactory().CreateLogger<RetryFailedTestsPipeServer>(),
            serviceProvider.GetTask(),
            maxNumberOfServerInstances: 1,
            serviceProvider.GetTestHostControllerAuthorizedSecurityIdentities(),
            serviceProvider.GetTestApplicationCancellationTokenSource().CancellationToken);

        _singleConnectionNamedPipeServer.RegisterSerializer(new VoidResponseSerializer(), typeof(VoidResponse));
        _singleConnectionNamedPipeServer.RegisterSerializer(new FailedTestRequestSerializer(), typeof(FailedTestRequest));
        _singleConnectionNamedPipeServer.RegisterSerializer(new GetListOfFailedTestsRequestSerializer(), typeof(GetListOfFailedTestsRequest));
        _singleConnectionNamedPipeServer.RegisterSerializer(new GetListOfFailedTestsResponseSerializer(), typeof(GetListOfFailedTestsResponse));
        _singleConnectionNamedPipeServer.RegisterSerializer(new TestRunCountsRequestSerializer(), typeof(TestRunCountsRequest));
        _singleConnectionNamedPipeServer.RegisterSerializer(new ArtifactRequestSerializer(), typeof(ArtifactRequest));
        _failedTests = failedTests;
    }

    public string PipeName => _singleConnectionNamedPipeServer.PipeName.Name;

    /// <summary>
    /// Gets the distinct uids of the tests that failed in this attempt, mapped to their display name.
    /// </summary>
    /// <remarks>
    /// Deliberately a dictionary keyed by uid rather than a flat list of every failed result: a folded data-driven
    /// test reports several results under a single uid, and the retry filter, the threshold policy and the summary
    /// all reason in terms of tests, not results. Counting results here inflated all three.
    /// </remarks>
    public Dictionary<string, string> FailedTests { get; } = [];

    /// <summary>
    /// Gets the number of test <em>results</em> that executed (skipped excluded) in this attempt.
    /// </summary>
    /// <remarks>
    /// Counted per result rather than per uid, matching the platform run summary, so that a folded data-driven test
    /// contributes one unit per data row. <see cref="FailedTests"/> is keyed by uid because it drives the retry
    /// filter; the two must therefore never be combined into a single ratio without care — see
    /// <see cref="FailedTestResults"/>.
    /// </remarks>
    public int TotalTestRan { get; private set; }

    /// <summary>
    /// Gets the number of failing test <em>results</em> in this attempt, i.e. the same unit as
    /// <see cref="TotalTestRan"/>. The failure-threshold policy uses this pair so its percentage stays consistent.
    /// </summary>
    public int FailedTestResults { get; private set; }

    /// <summary>
    /// Gets the number of skipped test results in this attempt. Reported separately from <see cref="TotalTestRan"/>
    /// (which counts only executed tests, as the failure-threshold policy requires) so the summary's "total" can
    /// include them and match the platform run summary.
    /// </summary>
    public int SkippedTests { get; private set; }

    /// <summary>
    /// Gets the uids of the tests this attempt was asked to retry which genuinely passed.
    /// </summary>
    public IReadOnlyList<string> RecoveredTests { get; private set; } = [];

    /// <summary>
    /// Gets a value indicating whether the attempt reported its counts at all. An attempt that dies before its test
    /// session finishes (crash, FailFast, abort) never sends them, leaving the counts at zero — which must not be
    /// mistaken for "a run of zero tests".
    /// </summary>
    public bool CountsReported { get; private set; }

    public List<ArtifactRequest> Artifacts { get; } = [];

    public Task WaitForConnectionAsync(CancellationToken cancellationToken)
        => _singleConnectionNamedPipeServer.WaitConnectionAsync(cancellationToken);

    public void Dispose()
        => _singleConnectionNamedPipeServer.Dispose();

    private Task<IResponse> CallbackAsync(IRequest request)
    {
        if (request is FailedTestRequest failed)
        {
            // Last writer wins on the display name; every result sharing a uid describes the same test node.
            FailedTests[failed.Uid] = failed.DisplayName;
            return Task.FromResult((IResponse)VoidResponse.CachedInstance);
        }

        if (request is GetListOfFailedTestsRequest)
        {
            return Task.FromResult((IResponse)new GetListOfFailedTestsResponse(_failedTests));
        }

        if (request is TestRunCountsRequest testRunCounts)
        {
            TotalTestRan = testRunCounts.ExecutedTests;
            FailedTestResults = testRunCounts.FailedTests;
            SkippedTests = testRunCounts.SkippedTests;
            RecoveredTests = testRunCounts.RecoveredTestUids;
            CountsReported = true;
            return Task.FromResult((IResponse)VoidResponse.CachedInstance);
        }

        if (request is ArtifactRequest artifact)
        {
            Artifacts.Add(artifact);
            return Task.FromResult((IResponse)VoidResponse.CachedInstance);
        }

        throw ApplicationStateGuard.Unreachable();
    }
}
