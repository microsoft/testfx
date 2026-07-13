// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable CS8618 // Properties below are set by MSBuild.

using Microsoft.Build.Framework;
using Microsoft.Testing.Extensions.MSBuild.Serializers;
using Microsoft.Testing.Platform.IPC;

using Task = System.Threading.Tasks.Task;

namespace Microsoft.Testing.Platform.MSBuild;

/// <summary>
/// Task that invokes the Testing Platform.
/// </summary>
public partial class InvokeTestingPlatformTask : Build.Utilities.ToolTask, IDisposable
{
    private const string MonoRunnerName = "mono";
    private static readonly string DotnetRunnerName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dotnet.exe" : "dotnet";

    private readonly IFileSystem _fileSystem;
    private readonly PipeNameDescription _pipeNameDescription;
    private readonly CancellationTokenSource _waitForConnections = new();
    private readonly List<NamedPipeServer> _connections = [];
    private readonly StringBuilder _output = new();

#if NET9_0_OR_GREATER
    private readonly Lock _initLock = new();
#else
    private readonly object _initLock = new();
#endif

    private readonly Architecture _currentProcessArchitecture = RuntimeInformation.ProcessArchitecture;

    private Task? _connectionLoopTask;
    private ModuleInfoRequest? _moduleInfo;
    private bool _receivedRunSummaryInfoRequest;
    private string? _outputFileName;
    private StreamWriter? _outputFileStream;
    private string? _toolCommand;
    private bool _captureOutput;
    private bool _showTestsFailure;

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
