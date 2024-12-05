// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable CS8618 // Properties below are set by MSBuild.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

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

using static Microsoft.Testing.Platform.MSBuild.Tasks.DotnetMuxerLocator;

using Task = System.Threading.Tasks.Task;

namespace Microsoft.Testing.Platform.MSBuild;

public class InvokeTestingPlatformTask : Build.Utilities.ToolTask, IDisposable
{
    private const string MonoRunnerName = "mono";
    private const string DotnetRunnerName = "dotnet";

    private readonly IFileSystem _fileSystem;
    private readonly PipeNameDescription _pipeNameDescription;
    private readonly CancellationTokenSource _waitForConnections = new();
    private readonly List<NamedPipeServer> _connections = new();
    private readonly StringBuilder _output = new();
    private readonly Lock _initLock = new();
    private readonly Process _currentProcess = Process.GetCurrentProcess();
    private readonly Architecture _currentProcessArchitecture = RuntimeInformation.ProcessArchitecture;

    private Task? _connectionLoopTask;
    private ModuleInfoRequest? _moduleInfo;
    private string _outputFileName;
    private StreamWriter? _outputFileStream;
    private string? _toolCommand;

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

    [Required]
    public ITaskItem TargetPath { get; set; }

    [Required]
    public ITaskItem TargetFramework { get; set; }

    [Required]
    public ITaskItem TestArchitecture { get; set; }

    [Required]
    public ITaskItem TargetFrameworkIdentifier { get; set; }

    [Required]
    public ITaskItem TestingPlatformShowTestsFailure { get; set; }

    [Required]
    public ITaskItem TestingPlatformCaptureOutput { get; set; }

    [Required]
    public ITaskItem ProjectFullPath { get; set; }

    public ITaskItem? DotnetHostPath { get; set; }

    public ITaskItem? TestingPlatformCommandLineArguments { get; set; }

    public ITaskItem[]? VSTestCLIRunSettings { get; set; }

    private bool IsNetCoreApp => TargetFrameworkIdentifier.ItemSpec == ".NETCoreApp";

    protected override string ToolName
    {
        get
        {
            // If target dll ends with .dll we're in the "dotnet" context
            if (TargetPath.ItemSpec.EndsWith(".dll", StringComparison.InvariantCultureIgnoreCase))
            {
                Log.LogMessage(MessageImportance.Low, $"Target path is a dll '{TargetPath.ItemSpec}'");
                return DotnetRunnerName + (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty);
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

    public override string ToolExe { get => base.ToolExe; set => throw new NotSupportedException(); }

    protected override string? GenerateFullPathToTool()
    {
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
            if (!IsCurrentProcessArchitectureCompatible())
            {
                Log.LogMessage(MessageImportance.Low, $"Current process architecture '{_currentProcessArchitecture}' is not compatible with '{TestArchitecture.ItemSpec}'");
                PlatformArchitecture targetArchitecture = EnumPolyfill.Parse<PlatformArchitecture>(TestArchitecture.ItemSpec, ignoreCase: true);
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
            else
            {
                if (DotnetHostPath is not null && File.Exists(DotnetHostPath.ItemSpec))
                {
                    Log.LogMessage(MessageImportance.Low, $"dotnet muxer tool path found using DOTNET_HOST_PATH environment variable: '{DotnetHostPath.ItemSpec}'");
                    return DotnetHostPath.ItemSpec;
                }

                ProcessModule? mainModule = _currentProcess.MainModule;
                if (mainModule != null && Path.GetFileName(mainModule.FileName)!.Equals(dotnetRunnerName, StringComparison.OrdinalIgnoreCase))
                {
                    Log.LogMessage(MessageImportance.Low, $"dotnet muxer tool path found using current process: '{mainModule.FileName}' architecture: '{_currentProcessArchitecture}'");
                    return mainModule.FileName;
                }
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
        _currentProcessArchitecture == EnumPolyfill.Parse<Architecture>(TestArchitecture.ItemSpec, ignoreCase: true);

    protected override string GenerateCommandLineCommands()
    {
        Build.Utilities.CommandLineBuilder builder = new();

        if (IsNetCoreApp)
        {
            string dotnetRunnerName = ToolName;
            if (dotnetRunnerName != MonoRunnerName && Path.GetFileName(_currentProcess.MainModule!.FileName!).Equals(dotnetRunnerName, StringComparison.OrdinalIgnoreCase))
            {
                builder.AppendSwitch("exec");
                builder.AppendFileNameIfNotNull(TargetPath.ItemSpec);
            }
        }
        else
        {
            // If the target is an exe and we're not on Windows we try with the mono runner and so we pass the test module to the mono runner.
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && TargetPath.ItemSpec.EndsWith(".exe", StringComparison.InvariantCultureIgnoreCase))
            {
                builder.AppendFileNameIfNotNull(TargetPath.ItemSpec);
            }
        }

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

    protected override bool ValidateParameters()
    {
        if (!_fileSystem.Exist(TargetPath.ItemSpec))
        {
            Log.LogError(Resources.MSBuildResources.InvalidTargetPath, TargetPath.ItemSpec);
            return false;
        }

        return true;
    }

    protected override MessageImportance StandardOutputLoggingImportance
        => MessageImportance.Low;

    protected override MessageImportance StandardErrorLoggingImportance
        => MessageImportance.Low;

    protected override void LogEventsFromTextOutput(string singleLine, MessageImportance messageImportance)
    {
        if (!bool.Parse(TestingPlatformCaptureOutput.ItemSpec))
        {
            Log.LogMessage(MessageImportance.High, singleLine);
        }

        // Collect the output to be written to the file.
        _output.AppendLine(singleLine);
    }

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
                    await pipeServer.WaitConnectionAsync(_waitForConnections.Token);
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

    protected override void LogToolCommand(string message)
    {
        _toolCommand = message;
        Log.LogMessage(MessageImportance.Low, $"Tool command: '{message}'");
        Log.LogMessage(MessageImportance.High, Resources.MSBuildResources.RunTests, TargetPath.ItemSpec.Trim(), TargetFramework.ItemSpec, TestArchitecture.ItemSpec);
    }

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
