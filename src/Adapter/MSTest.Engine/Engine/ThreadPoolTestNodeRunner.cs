// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Framework.Configurations;
using Microsoft.Testing.Framework.Helpers;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.TestHost;

using PlatformTestNode = Microsoft.Testing.Platform.Extensions.Messages.TestNode;

namespace Microsoft.Testing.Framework;

internal sealed class ThreadPoolTestNodeRunner : IDisposable
{
    private readonly SemaphoreSlim? _maxParallelTests;
    private readonly ConcurrentBag<Task<Result>> _runningTests = new();
    private readonly ConcurrentDictionary<TestNodeUid, int> _runningTestNodeUids = new();
    private readonly CountdownEvent _ensureTaskQueuedCountdownEvent = new(1);
    private readonly Func<IData, Task> _publishDataAsync;
    private readonly TestFixtureManager _testFixtureManager;
    private readonly CancellationToken _cancellationToken;
    private readonly IClock _clock;
    private readonly IConfiguration _configuration;
    private readonly SessionUid _sessionUid;
    private readonly ITask _task;
    private readonly ITrxReportCapability? _trxReportCapability;
    private readonly TaskCompletionSource<int> _waitForStart = new();
    private bool _isDisposed;

    public ThreadPoolTestNodeRunner(TestFrameworkConfiguration testFrameworkConfiguration, ITestFrameworkCapabilities capabilities, IClock clock, ITask task, IConfiguration configuration,
        SessionUid sessionUid, Func<IData, Task> publishDataAsync, TestFixtureManager testFixtureManager,
        CancellationToken cancellationToken)
    {
        _clock = clock;
        _configuration = configuration;
        _sessionUid = sessionUid;
        _publishDataAsync = publishDataAsync;
        _testFixtureManager = testFixtureManager;
        _cancellationToken = cancellationToken;
        _task = task;
        _trxReportCapability = capabilities.GetCapability<ITrxReportCapability>();
        if (testFrameworkConfiguration.MaxParallelTests != int.MaxValue)
        {
            _maxParallelTests = new SemaphoreSlim(testFrameworkConfiguration.MaxParallelTests);
        }

        cancellationToken.Register(_waitForStart.SetCanceled);
    }

    public void EnqueueTest(TestNode frameworkTestNode, TestNodeUid? parentTestNodeUid)
    {
        _ensureTaskQueuedCountdownEvent.AddCount();
        try
        {
            _runningTests.Add(
                _task.Run(
                    async () =>
                    {
                        try
                        {
                            // We don't have a timeout here because we can have really slow fixture and it's on user
                            // the decision on how much to wait for it.
                            await _waitForStart.Task;

                            // Handle the global parallelism.
                            if (_maxParallelTests is not null)
                            {
                                await _maxParallelTests.WaitAsync();
                            }

                            try
                            {
                                _runningTestNodeUids.AddOrUpdate(frameworkTestNode.StableUid, 1, (_, count) => count + 1);

                                PlatformTestNode progressNode = frameworkTestNode.ToPlatformTestNode();
                                progressNode.Properties.Add(InProgressTestNodeStateProperty.CachedInstance);
                                await _publishDataAsync(new TestNodeUpdateMessage(_sessionUid, progressNode, parentTestNodeUid?.ToPlatformTestNodeUid()));

                                Result result = await CreateTestRunTaskAsync(frameworkTestNode, parentTestNodeUid);

                                _runningTestNodeUids.TryRemove(frameworkTestNode.StableUid, out int count);

                                return count > 1
                                    ? throw new InvalidOperationException($"Test node '{frameworkTestNode.StableUid}' was run {count} times")
                                    : result;
                            }
                            finally
                            {
                                _maxParallelTests?.Release();
                            }
                        }
                        catch (Exception ex)
                        {
                            Environment.FailFast($"Unhandled exception inside '{nameof(CreateTestRunTaskAsync)}'", ex);
                            throw;
                        }
                    },
                    _cancellationToken));
        }
        catch (OperationCanceledException ex) when (ex.CancellationToken == _cancellationToken)
        {
            // We are being cancelled, so we don't need to wait anymore
        }
        finally
        {
            // We will signal for the second counting inside CreateTestRunTaskAsync() after the test run.
            _ensureTaskQueuedCountdownEvent.Signal();
        }
    }

    public void StartTests()
        => _waitForStart.SetResult(0);

    private async Task<Result> CreateTestRunTaskAsync(TestNode testNode, TestNodeUid? parentTestNodeUid)
    {
        try
        {
            await _testFixtureManager.SetupUsedFixturesAsync(testNode);
        }
        catch (Exception ex)
        {
            StringBuilder errorBuilder = new();
            errorBuilder.AppendLine(CultureInfo.InvariantCulture, $"Error while initializing fixtures for test '{testNode.DisplayName}' (UID = {testNode.StableUid.Value})");
            errorBuilder.AppendLine();
            errorBuilder.AppendLine(ex.ToString());
            return Result.Fail(errorBuilder.ToString());
        }

        Result result = await InvokeTestNodeAndPublishResultAsync(testNode, parentTestNodeUid,
            async (testNode, testExecutionContext) =>
            {
                switch (testNode)
                {
                    case IAsyncActionTestNode actionTestNode:
                        await actionTestNode.InvokeAsync(testExecutionContext);
                        break;

                    case IActionTestNode actionTestNode:
                        actionTestNode.Invoke(testExecutionContext);
                        break;

                    case IParameterizedAsyncActionTestNode actionTestNode:
                        await actionTestNode.InvokeAsync(
                            testExecutionContext,
                            action => InvokeTestNodeAndPublishResultAsync(testNode, parentTestNodeUid, (_, _) => action(), skipPublishResult: false));
                        break;

                    default:
                        break;
                }
            },
            // Because parameterized tests report multiple results (one per parameter set), we don't want to publish the result
            // of the overall test node execution, but only the results of the individual parameterized tests.
            skipPublishResult: testNode is IParameterizedAsyncActionTestNode);

        // Try to cleanup the fixture is not more used.
        try
        {
            await _testFixtureManager.CleanUnusedFixturesAsync(testNode);
            return result;
        }
        catch (Exception ex)
        {
            StringBuilder errorBuilder = new();
            errorBuilder.AppendLine(CultureInfo.InvariantCulture, $"Error while cleaning fixtures for test '{testNode.StableUid}'");
            errorBuilder.AppendLine();
            errorBuilder.AppendLine(ex.ToString());
            return result.WithError(errorBuilder.ToString());
        }
    }

    public async Task<Result> WaitAllTestsAsync(CancellationToken cancellationToken)
    {
        try
        {
            _ensureTaskQueuedCountdownEvent.Signal();
            await _ensureTaskQueuedCountdownEvent.WaitAsync(cancellationToken);
            Result[] results = await Task.WhenAll(_runningTests);
            return Result.Combine(results);
        }
        catch (OperationCanceledException ex) when (ex.CancellationToken == cancellationToken)
        {
            // If the cancellation token is triggered, we don't want to report the cancellation as a failure
            return Result.Ok("Cancelled by user");
        }
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _ensureTaskQueuedCountdownEvent.Dispose();
            _isDisposed = true;
        }
    }

    private async Task<Result> InvokeTestNodeAndPublishResultAsync(TestNode testNode, TestNodeUid? parentTestNodeUid,
        Func<TestNode, ITestExecutionContext, Task> testNodeInvokeAction, bool skipPublishResult)
    {
        TimeSheet timesheet = new(_clock);
        timesheet.RecordStart();

        PlatformTestNode platformTestNode = testNode.ToPlatformTestNode();

        if (_trxReportCapability is not null && _trxReportCapability.IsSupported)
        {
            platformTestNode.Properties.Add(new TrxFullyQualifiedTypeNameProperty(platformTestNode.Uid.Value[..platformTestNode.Uid.Value.LastIndexOf('.')]));
        }

        TestExecutionContext testExecutionContext = new(_configuration, testNode, platformTestNode, _trxReportCapability, _cancellationToken);
        try
        {
            // If we're already enqueued we cancel the test before the start
            // The test could not use the cancellation and we should wait the end of the test self to cancel.
            _cancellationToken.ThrowIfCancellationRequested();
            await testNodeInvokeAction(testNode, testExecutionContext);

            if (!platformTestNode.Properties.Any<TestNodeStateProperty>())
            {
                platformTestNode.Properties.Add(PassedTestNodeStateProperty.CachedInstance);
            }
        }
        catch (MissingMethodException ex)
        {
            // In dotnet watch mode we can remove tests.
            if (Environment.GetEnvironmentVariable("DOTNET_WATCH") == "1")
            {
                return Result.Ok().WithWarning("Under 'DOTNET_WATCH' cannot find some member." + Environment.NewLine + ex.StackTrace);
            }

            throw;
        }
        catch (Exception ex)
        {
            testExecutionContext.ReportException(ex);
        }
        finally
        {
            timesheet.RecordStop();
            platformTestNode.Properties.Add(new TimingProperty(new TimingInfo(timesheet.StartTime, timesheet.StopTime, timesheet.Duration)));
        }

        if (!skipPublishResult)
        {
            await _publishDataAsync(new TestNodeUpdateMessage(_sessionUid, platformTestNode, parentTestNodeUid?.ToPlatformTestNodeUid()));
        }

        return Result.Ok();
    }
}
