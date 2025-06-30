// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable CS8618 // Properties below are set by MSBuild.

using Microsoft.Build.Framework;
using Microsoft.Testing.Extensions.MSBuild;
using Microsoft.Testing.Extensions.MSBuild.Serializers;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.IPC.Serializers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.MSBuild.Tasks;
using Microsoft.Testing.Platform.OutputDevice;

using static Microsoft.Testing.Platform.MSBuild.Tasks.DotnetMuxerLocator;

using Task = System.Threading.Tasks.Task;

namespace Microsoft.Testing.Platform.MSBuild;

/// <summary>
/// Task that invokes the Testing Platform.
/// </summary>
public class InvokeTestingPlatformTask : Build.Utilities.ToolTask, IDisposable
{
    private const string MonoRunnerName = "mono";
    private static readonly string DotnetRunnerName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dotnet.exe" : "dotnet";

    private readonly IFileSystem _fileSystem;
    private readonly PipeNameDescription _pipeNameDescription;
    private readonly CancellationTokenSource _waitForConnections = new();
    private readonly List<NamedPipeServer> _connections = [];
    private readonly StringBuilder _output = new();
    private readonly Lock _initLock = new();
    private readonly Architecture _currentProcessArchitecture = RuntimeInformation.ProcessArchitecture;

    private Task? _connectionLoopTask;
    private ModuleInfoRequest? _moduleInfo;
    private string? _outputFileName;
    private StreamWriter? _outputFileStream;
    private string? _toolCommand;

    /// <summary>
    /// Initializes a new instance of the <see cref="InvokeTestingPlatformTask"/> class.
    /// </summary>
    public InvokeTestingPlatformTask()
       : this(new FileSystem())
    {
        if (Environment.GetEnvironmentVariable("TESTINGPLATFORM_MSBUILD_LAUNCH_ATTACH_DEBUGGER") == "1")
        {
            Debugger.Launch();
        }

        _pipeNameDescription = NamedPipeServer.GetPipeName(Guid.NewGuid().ToString("N"));
    }

    internal InvokeTestingPlatformTask(IFileSystem fileSystem) => _fileSystem = fileSystem;

    /// <summary>
    /// Gets or sets the target path.
    /// </summary>
    [Required]
    public ITaskItem TargetPath { get; set; }

    // -------- BEGIN the following properties shouldn't be used. See https://github.com/microsoft/testfx/issues/5091 --------

    /// <summary>
    /// Gets or sets the value of MSBuild property UseAppHost.
    /// </summary>
    public ITaskItem? UseAppHost { get; set; }

    /// <summary>
    /// Gets or sets the value of MSBuild property _IsExecutable.
    /// </summary>
    public ITaskItem? IsExecutable { get; set; }

    /// <summary>
    /// Gets or sets the value of MSBuild property TargetDir.
    /// </summary>
    public ITaskItem? TargetDir { get; set; }

    /// <summary>
    /// Gets or sets the value of MSBuild property AssemblyName.
    /// </summary>
    public ITaskItem? AssemblyName { get; set; }

    /// <summary>
    /// Gets or sets the value of MSBuild property _NativeExecutableExtension.
    /// </summary>
    public ITaskItem? NativeExecutableExtension { get; set; }

    // -------- END the previous properties shouldn't be used. See https://github.com/microsoft/testfx/issues/5091 --------

    /// <summary>
    /// Gets or sets the target framework.
    /// </summary>
    [Required]
    public ITaskItem TargetFramework { get; set; }

    /// <summary>
    /// Gets or sets the test architecture.
    /// </summary>
    [Required]
    public ITaskItem TestArchitecture { get; set; }

    /// <summary>
    /// Gets or sets the target framework identifier.
    /// </summary>
    [Required]
    public ITaskItem TargetFrameworkIdentifier { get; set; }

    /// <summary>
    /// Gets or sets the testing platform show tests failure.
    /// </summary>
    [Required]
    public ITaskItem TestingPlatformShowTestsFailure { get; set; }

    /// <summary>
    /// Gets or sets the testing platform capture output.
    /// </summary>
    [Required]
    public ITaskItem TestingPlatformCaptureOutput { get; set; }

    /// <summary>
    /// Gets or sets the project full path.
    /// </summary>
    [Required]
    public ITaskItem ProjectFullPath { get; set; }

    /// <summary>
    /// Gets or sets the dotnet host path.
    /// </summary>
    public ITaskItem? DotnetHostPath { get; set; }

    /// <summary>
    /// Gets or sets the testing platform command line arguments.
    /// </summary>
    public ITaskItem? TestingPlatformCommandLineArguments { get; set; }

    /// <summary>
    /// Gets or sets the VSTestCLI run settings.
    /// </summary>
    public ITaskItem[]? VSTestCLIRunSettings { get; set; }

    private bool IsNetCoreApp => TargetFrameworkIdentifier.ItemSpec == ".NETCoreApp";

    /// <inheritdoc />
    protected override string ToolName
    {
        get
        {
            if (TryGetRunCommand() is string runCommand)
            {
                Log.LogMessage(MessageImportance.Low, $"Constructed target path via similar logic as to RunCommand: '{runCommand}'");
                return Path.GetFileName(runCommand);
            }

            // If target dll ends with .dll we're in the "dotnet" context
            if (TargetPath.ItemSpec.EndsWith(".dll", StringComparison.InvariantCultureIgnoreCase))
            {
                Log.LogMessage(MessageImportance.Low, $"Target path is a dll '{TargetPath.ItemSpec}'");
                return DotnetRunnerName;
            }

            // If the target is an exe and we're not on Windows we try with the mono runner.
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && TargetPath.ItemSpec.EndsWith(".exe", StringComparison.InvariantCultureIgnoreCase))
            {
                Log.LogMessage(MessageImportance.Low, $"Target is an '.exe' on '{RuntimeInformation.OSDescription}' trying with the mono runner.");
                return MonoRunnerName;
            }

            return Path.GetFileName(TargetPath.ItemSpec);
        }
    }

    /// <inheritdoc />
    public override string ToolExe { get => base.ToolExe; set => throw new NotSupportedException(); }

    /// <inheritdoc />
    protected override string? GenerateFullPathToTool()
    {
        if (TryGetRunCommand() is string runCommand)
        {
            return runCommand;
        }

        // If it's not netcore and we're on Windows we expect the TargetPath to be the executable, otherwise we try with mono.
        if (!IsNetCoreApp && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Log.LogMessage(MessageImportance.Low, $"Test application is not a .NETCoreApp, tool path: '{TargetPath.ItemSpec}'");
            return TargetPath.ItemSpec;
        }

        string dotnetRunnerName = ToolName;
        Log.LogMessage(MessageImportance.Low, $"Tool name: '{dotnetRunnerName}'");

        // We look for dotnet muxer only if we're not running with mono.
        if (dotnetRunnerName != MonoRunnerName)
        {
            if (DotnetHostPath is not null && File.Exists(DotnetHostPath.ItemSpec) && IsCurrentProcessArchitectureCompatible())
            {
                Log.LogMessage(MessageImportance.Low, $"dotnet muxer tool path found using DOTNET_HOST_PATH environment variable: '{DotnetHostPath.ItemSpec}'");
                return DotnetHostPath.ItemSpec;
            }

            Log.LogMessage(MessageImportance.Low, $"Current process architecture '{_currentProcessArchitecture}'. Requested test architecture '{TestArchitecture.ItemSpec}'");
            PlatformArchitecture targetArchitecture = Enum.Parse<PlatformArchitecture>(TestArchitecture.ItemSpec, ignoreCase: true);
            StringBuilder resolutionLog = new();
            DotnetMuxerLocator dotnetMuxerLocator = new(log => resolutionLog.AppendLine(log));
            if (dotnetMuxerLocator.TryGetDotnetPathByArchitecture(targetArchitecture, out string? dotnetPath))
            {
                Log.LogMessage(MessageImportance.Low, resolutionLog.ToString());
                Log.LogMessage(MessageImportance.Low, $"dotnet muxer tool path found using architecture: '{TestArchitecture.ItemSpec}' '{dotnetPath}'");
                return dotnetPath;
            }
            else
            {
                Log.LogMessage(MessageImportance.Low, resolutionLog.ToString());
                Log.LogError(string.Format(CultureInfo.InvariantCulture, Resources.MSBuildResources.IncompatibleArchitecture, dotnetRunnerName, TestArchitecture.ItemSpec));
                return null;
            }
        }

        string values = Environment.GetEnvironmentVariable("PATH")!;
        foreach (string? p in values.Split(Path.PathSeparator))
        {
            string fullPath = Path.Combine(p, dotnetRunnerName);
            if (File.Exists(fullPath))
            {
                Log.LogMessage(MessageImportance.Low, $"Runner tool path found using PATH environment variable: '{fullPath}'");
                return fullPath;
            }
        }

        Log.LogError(Resources.MSBuildResources.FullPathToolCalculationFailed, dotnetRunnerName);

        return null;
    }

    private bool IsCurrentProcessArchitectureCompatible() =>
        _currentProcessArchitecture == Enum.Parse<Architecture>(TestArchitecture.ItemSpec, ignoreCase: true);

    private string? TryGetRunCommand()
    {
        // This condition specifically handles this part:
        // https://github.com/dotnet/sdk/blob/5846d648f2280b54a54e481f55de4d9eea0e6a0e/src/Tasks/Microsoft.NET.Build.Tasks/targets/Microsoft.NET.Sdk.targets#L1152-L1155
        // The more correct logic is implementing https://github.com/microsoft/testfx/issues/5091
        // What we want to do here is to avoid using 'dotnet exec' if possible, and run the executable directly instead.
        // When running with dotnet exec, we are run under dotnet.exe process, which can break some scenarios (e.g, loading PRI in WinUI tests).
        // It seems like WinUI would try to resolve the PRI file relative to the path of the process, so relative to, e.g, C:\Program Files\dotnet
        if (IsNetCoreApp &&
            bool.TryParse(IsExecutable?.ItemSpec, out bool isExecutable) && isExecutable &&
            bool.TryParse(UseAppHost?.ItemSpec, out bool useAppHost) && useAppHost)
        {
            string runCommand = $"{TargetDir?.ItemSpec}{AssemblyName?.ItemSpec}{NativeExecutableExtension?.ItemSpec}";
            if (File.Exists(runCommand))
            {
                return runCommand;
            }
        }

        return null;
    }

    /// <inheritdoc />
    protected override string GenerateCommandLineCommands()
    {
        Build.Utilities.CommandLineBuilder builder = new();

        if (ToolName == DotnetRunnerName && IsNetCoreApp)
        {
            // In most cases, if ToolName is "dotnet.exe", that means we are given a "dll" file.
            // In turn, that means we are not .NET Framework (because we will use Exe otherwise).
            // In case ToolName ended up being "dotnet.exe" and we are
            // .NET Framework, that means it's the user's assembly that is named "dotnet".
            // In that case, we want to execute the tool (user's executable) directly.
            // So, we only only "exec" if we are .NETCoreApp
            builder.AppendSwitch("exec");
            builder.AppendFileNameIfNotNull(TargetPath.ItemSpec);
        }
        else if (ToolName == MonoRunnerName)
        {
            // If ToolName is "mono", that means TargetPath is an "exe" file and we are not running on Windows.
            // In this case, we use the given exe file as an argument to mono.
            builder.AppendFileNameIfNotNull(TargetPath.ItemSpec);
        }

        // If we are not "dotnet.exe" and not "mono", then we are given an executable from user and we are running on Windows.
        builder.AppendSwitchIfNotNull($"--{MSBuildConstants.MSBuildNodeOptionKey} ", _pipeNameDescription.Name);

        if (!string.IsNullOrEmpty(TestingPlatformCommandLineArguments?.ItemSpec))
        {
            builder.AppendTextUnquoted($" {TestingPlatformCommandLineArguments!.ItemSpec} ");
        }

        if (VSTestCLIRunSettings?.Length > 0)
        {
            foreach (ITaskItem taskItem in VSTestCLIRunSettings)
            {
                builder.AppendTextUnquoted($" {taskItem.ItemSpec}");
            }
        }

        return builder.ToString();
    }

    /// <inheritdoc />
    protected override bool ValidateParameters()
    {
        if (!_fileSystem.Exist(TargetPath.ItemSpec))
        {
            Log.LogError(Resources.MSBuildResources.InvalidTargetPath, TargetPath.ItemSpec);
            return false;
        }

        return true;
    }

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
            StringBuilder sb = new();
            sb.AppendLine();
            sb.AppendLine("=== COMMAND LINE ===");
            sb.AppendLine(_toolCommand);
            _output.AppendLine(sb.ToString());
        }

        // Persist the output to the file.
        _outputFileStream?.WriteLine(_output);

        _waitForConnections.Cancel();
        Dispose();

        if (returnValue)
        {
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
        if (string.IsNullOrEmpty(_outputFileName) && ExitCode != ExitCodes.InvalidCommandLine)
        {
            Log.LogError(null, "run failed", null, TargetPath.ItemSpec.Trim(), 0, 0, 0, 0, Resources.MSBuildResources.TestFailedNoDetail, _output);
        }
        else
        {
            // If the output file name is null and the exit code is invalid command line we create a default one.
            if (_outputFileName is null && ExitCode == ExitCodes.InvalidCommandLine)
            {
                _outputFileName = Path.Combine(Path.GetDirectoryName(TargetPath.ItemSpec.Trim())!, AggregatedConfiguration.DefaultTestResultFolderName);
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
            string summary = string.Format(
                CultureInfo.CurrentCulture,
                Resources.MSBuildResources.Summary,
                runSummaryInfoRequest.TotalFailed > 0 || runSummaryInfoRequest.TotalPassed == 0
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

    /// <inheritdoc />
    public void Dispose()
    {
        _outputFileStream?.Dispose();
        _waitForConnections.Cancel();
        _connectionLoopTask?.Wait();

        foreach (NamedPipeServer serverInstance in _connections)
        {
            serverInstance.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}
