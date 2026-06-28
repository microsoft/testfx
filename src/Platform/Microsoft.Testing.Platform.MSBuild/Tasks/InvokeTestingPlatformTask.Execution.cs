// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Testing.Extensions.MSBuild.Serializers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.IPC.Serializers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;

using Task = System.Threading.Tasks.Task;

namespace Microsoft.Testing.Platform.MSBuild;

public partial class InvokeTestingPlatformTask
{
    /// <inheritdoc />
    protected override MessageImportance StandardOutputLoggingImportance
        => MessageImportance.Low;

    /// <inheritdoc />
    protected override MessageImportance StandardErrorLoggingImportance
        => MessageImportance.Low;

    /// <inheritdoc />
    protected override void LogEventsFromTextOutput(string singleLine, MessageImportance messageImportance)
    {
        if (!bool.Parse(TestingPlatformCaptureOutput.ItemSpec))
        {
            Log.LogMessage(MessageImportance.High, singleLine);
        }

        // Collect the output to be written to the file.
        _output.AppendLine(singleLine);
    }

    /// <inheritdoc />
    protected override void ProcessStarted()
        => _connectionLoopTask = Task.Run(async () =>
        {
            try
            {
                while (!_waitForConnections.IsCancellationRequested)
                {
                    NamedPipeServer pipeServer = new(_pipeNameDescription, HandleRequestAsync, new SystemEnvironment(), new MSBuildLogger(), new SystemTask(), maxNumberOfServerInstances: 100, CancellationToken.None);
                    pipeServer.RegisterSerializer(new ModuleInfoRequestSerializer(), typeof(ModuleInfoRequest));
                    pipeServer.RegisterSerializer(new VoidResponseSerializer(), typeof(VoidResponse));
                    pipeServer.RegisterSerializer(new FailedTestInfoRequestSerializer(), typeof(FailedTestInfoRequest));
                    pipeServer.RegisterSerializer(new RunSummaryInfoRequestSerializer(), typeof(RunSummaryInfoRequest));
                    await pipeServer.WaitConnectionAsync(_waitForConnections.Token).ConfigureAwait(false);
                    _connections.Add(pipeServer);
                    Log.LogMessage(MessageImportance.Low, $"Client connected to '{_pipeNameDescription.Name}'");
                }
            }
            catch (OperationCanceledException) when (_waitForConnections.IsCancellationRequested)
            {
                // Do nothing we're canceling
            }
            catch (Exception ex)
            {
                Log.LogError(ex.ToString());
            }
        });

    /// <inheritdoc />
    public override bool Execute()
    {
        bool returnValue = base.Execute();
        if (_toolCommand is not null)
        {
            _output.AppendLine();
            _output.AppendLine("=== COMMAND LINE ===");
            _output.AppendLine(_toolCommand);
        }

        // Persist the output to the file.
        _outputFileStream?.WriteLine(_output);

        _waitForConnections.Cancel();
        Dispose();

        if (returnValue)
        {
            if (_moduleInfo is null)
            {
                Log.LogError(Resources.MSBuildResources.DidNotReceiveModuleInfo, TargetPath.ItemSpec.Trim());
                return false;
            }

            if (!_receivedRunSummaryInfoRequest)
            {
                Log.LogError(Resources.MSBuildResources.DidNotReceiveRunSummaryInfo, TargetPath.ItemSpec.Trim());
                return false;
            }

            Log.LogMessage(MessageImportance.High, Resources.MSBuildResources.TestsSucceeded, TargetPath.ItemSpec.Trim(), TargetFramework.ItemSpec, TestArchitecture.ItemSpec);
        }

        return returnValue;
    }

    /// <inheritdoc />
    protected override void LogToolCommand(string message)
    {
        _toolCommand = message;
        Log.LogMessage(MessageImportance.Low, $"Tool command: '{message}'");
        Log.LogMessage(MessageImportance.High, Resources.MSBuildResources.RunTests, TargetPath.ItemSpec.Trim(), TargetFramework.ItemSpec, TestArchitecture.ItemSpec);
    }

    /// <inheritdoc />
    protected override bool HandleTaskExecutionErrors()
    {
        // This is an unexpected situation we simply print to the console the output and return false.
        if (string.IsNullOrEmpty(_outputFileName) && ExitCode != (int)Helpers.ExitCode.InvalidCommandLine)
        {
            Log.LogError(null, "run failed", null, TargetPath.ItemSpec.Trim(), 0, 0, 0, 0, Resources.MSBuildResources.TestFailedNoDetail, _output);
        }
        else
        {
            // If the output file name is null and the exit code is invalid command line we create a default one.
            if (_outputFileName is null && ExitCode == (int)Helpers.ExitCode.InvalidCommandLine)
            {
                _outputFileName = Path.Combine(Path.GetDirectoryName(TargetPath.ItemSpec.Trim())!, "TestResults");
                _fileSystem.CreateDirectory(_outputFileName);
                _outputFileName = Path.Combine(_outputFileName, $"{Path.GetFileNameWithoutExtension(TargetPath.ItemSpec.Trim())}_{TargetFramework.ItemSpec}_{TestArchitecture.ItemSpec}.log");
                Log.LogMessage(MessageImportance.Low, $"Invalid command line exit code and empty output file name, creating default one '{_outputFileName}'");
                _outputFileStream = new StreamWriter(_fileSystem.CreateNew(_outputFileName), Encoding.Unicode)
                {
                    AutoFlush = true,
                };
            }

            Log.LogError(null, "run failed", null, TargetPath.ItemSpec.Trim(), 0, 0, 0, 0, Resources.MSBuildResources.TestFailed, _outputFileName, TargetFramework.ItemSpec, TestArchitecture.ItemSpec);
        }

        return false;
    }

    private Task<IResponse> HandleRequestAsync(IRequest request)
    {
        // For the case, of orchestrator (e.g, Retry), we can get ModuleInfoRequest from the orchestrator itself.
        // If there is no orchestrator or the orchestrator didn't send ModuleInfoRequest, we will get it from the first test host.
        // For the case of retry, the request is different between the orchestrator and the test host.
        // More specifically, the results directory is different (orchestrator points to original, while test host points to the specific retry results directory).
        if (request is ModuleInfoRequest moduleInfo)
        {
            if (_moduleInfo is null)
            {
                lock (_initLock)
                {
                    if (_moduleInfo is null)
                    {
                        _moduleInfo = moduleInfo;
                        _outputFileName = $"{Path.GetFileNameWithoutExtension(TargetPath.ItemSpec.Trim())}_{TargetFramework.ItemSpec}_{TestArchitecture.ItemSpec}.log";
                        _outputFileName = Path.Combine(_moduleInfo.TestResultFolder, _outputFileName);
                        Log.LogMessage(MessageImportance.Low, $"Initializing module info and output file '{_outputFileName}'");
                        _outputFileStream = new StreamWriter(_fileSystem.CreateNew(_outputFileName), Encoding.Unicode)
                        {
                            AutoFlush = true,
                        };
                    }
                }
            }

            return Task.FromResult<IResponse>(VoidResponse.CachedInstance);
        }

        if (request is FailedTestInfoRequest failedTestInfoRequest)
        {
            // TestingPlatformShowTestsFailure is not enabled, don't write errors to output.
            if (!bool.Parse(TestingPlatformShowTestsFailure.ItemSpec))
            {
                return Task.FromResult<IResponse>(VoidResponse.CachedInstance);
            }

            failedTestInfoRequest.FromFailedTest(outputSupportsMultiline: MSBuildCompatibilityHelper.SupportsMultiLine(), TargetPath.ItemSpec.Trim(),
                out string errorCode, out string file, out int lineNumber, out string message, out string? lowPriorityMessage);
            Log.LogError(null, errorCode, null, file, lineNumber, 0, 0, 0, message, null);
            if (lowPriorityMessage is not null)
            {
                Log.LogMessage(MessageImportance.Low, lowPriorityMessage);
            }

            return Task.FromResult<IResponse>(VoidResponse.CachedInstance);
        }

        if (request is RunSummaryInfoRequest runSummaryInfoRequest)
        {
            // DESIGN: how an all-skipped (or zero-test) run is reported follows the `--zero-tests-policy` option
            // (#9385), resolved on the test-host side and carried here via `RunSummaryInfoRequest.AllowSkipped`:
            //   - `allow-skipped` (the default): only a run that discovered nothing at all (`Total == 0`) is reported
            //     as `Failed!`; an all-skipped run is reported as `Passed!`.
            //   - `strict`: an all-skipped (or zero-test) run (`TotalPassed == 0`) is reported as `Failed!`. This is
            //     the original behavior from #3216 / #3243 ("Skipped tests count as not run").
            //
            // Two sibling sites mirror this decision and must stay in lockstep:
            //   - TestApplicationResult.ConsumeAsync (excludes skipped from `_totalRanTests` -> exit code 8, honoring --zero-tests-policy)
            //   - TerminalTestReporter.Summary.cs / TestRunSummaryHelper (`allTestsWereSkipped` -> red "Zero tests ran", honoring --zero-tests-policy)
            // Do NOT change this verdict without revisiting those sites and the design discussion above.
            bool runFailed = runSummaryInfoRequest.TotalFailed > 0
                || (runSummaryInfoRequest.AllowSkipped
                    ? runSummaryInfoRequest.Total == 0
                    : runSummaryInfoRequest.TotalPassed == 0);
            string summary = string.Format(
                CultureInfo.CurrentCulture,
                Resources.MSBuildResources.Summary,
                runFailed
                    ? Resources.MSBuildResources.Failed
                    : Resources.MSBuildResources.Passed,
                runSummaryInfoRequest.TotalFailed,
                runSummaryInfoRequest.TotalPassed,
                runSummaryInfoRequest.TotalSkipped,
                runSummaryInfoRequest.Total,
                runSummaryInfoRequest.Duration);

            summary += $" - {Path.GetFileName(TargetPath.ItemSpec)} ({TargetFrameworkParser.GetShortTargetFramework(TargetFramework.ItemSpec)}|{TestArchitecture.ItemSpec})";

            if (MSBuildCompatibilityHelper.SupportsTerminalLoggerWithExtendedMessages())
            {
                var metadata = new Dictionary<string, string?>
                {
                    ["total"] = runSummaryInfoRequest.Total.ToString(CultureInfo.InvariantCulture),
                    ["passed"] = runSummaryInfoRequest.TotalPassed.ToString(CultureInfo.InvariantCulture),
                    ["skipped"] = runSummaryInfoRequest.TotalSkipped.ToString(CultureInfo.InvariantCulture),
                    ["failed"] = runSummaryInfoRequest.TotalFailed.ToString(CultureInfo.InvariantCulture),
                    ["duration"] = runSummaryInfoRequest.Duration,
                };

                // No need to check the return value, we checked explicitly that the api is supported in the if above.
                _ = MSBuildCompatibilityHelper.TryWriteExtendedMessage(BuildEngine, "TLTESTFINISH", summary, metadata);
            }
            else
            {
                Log.LogMessage(MessageImportance.High, summary);
            }

            _receivedRunSummaryInfoRequest = true;
            return Task.FromResult<IResponse>(VoidResponse.CachedInstance);
        }

        throw new NotImplementedException($"Request '{request.GetType()}' not supported.");
    }

    private sealed class MSBuildLogger : Logging.ILogger
    {
        public bool IsEnabled(LogLevel logLevel) => false;

        public void Log<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
        }

        public Task LogAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Task.CompletedTask;
    }
}
