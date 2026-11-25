// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Extensions.TestHostOrchestrator;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;

#if NETCOREAPP
using System.Security.AccessControl;
using System.Security.Principal;
#endif

namespace Microsoft.Testing.Platform.TestHostOrchestrator;

[UnsupportedOSPlatform("browser")]
[UnsupportedOSPlatform("wasi")]
[UnsupportedOSPlatform("ios")]
[UnsupportedOSPlatform("tvos")]
internal sealed class PartitioningOrchestrator(IServiceProvider serviceProvider)
    : IComposableTestHostExecutionOrchestrator, IOutputDeviceDataProducer
{
    public string Uid => nameof(PartitioningOrchestrator);

    public string Version => PartitioningExtension.Instance.Version;

    public string DisplayName => PartitioningExtension.Instance.DisplayName;

    public string Description => PlatformResources.PartitionCountOptionDescription;

    public TestHostExecutionOrchestratorStage Stage => TestHostExecutionOrchestratorStage.Distribution;

    public Task<bool> IsEnabledAsync()
        => Task.FromResult(
            PartitioningCommandLineOptionsProvider.IsSupportedBy(serviceProvider.GetService<ITestFrameworkCapabilities>())
            && serviceProvider.GetCommandLineOptions().IsOptionSet(PartitioningCommandLineOptionsProvider.PartitionCountOptionName)
            && !serviceProvider.GetCommandLineOptions().IsOptionSet(PartitioningCommandLineOptionsProvider.PartitionOptionName));

    [SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "Processor count bounds the local process worker pool.")]
    public async Task<int> OrchestrateTestHostExecutionAsync(CancellationToken cancellationToken)
    {
        IEnvironment environment = serviceProvider.GetEnvironment();
        if (serviceProvider.GetService<ITestHostLauncher>() is not null
            || environment.GetEnvironmentVariable(EnvironmentVariableConstants.DOTNET_WATCH) == "1"
            || environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_HOTRELOAD_ENABLED) == "1")
        {
            await serviceProvider.GetOutputDevice().DisplayAsync(
                this, new ErrorMessageOutputDeviceData(PlatformResources.PartitionCountUnsupportedMode), cancellationToken).ConfigureAwait(false);
            return (int)ExitCode.InvalidCommandLine;
        }

        serviceProvider.GetCommandLineOptions().TryGetOptionArgumentList(PartitioningCommandLineOptionsProvider.PartitionCountOptionName, out string[]? values);
        int count = int.Parse(values![0], CultureInfo.InvariantCulture);
        ExecutableInfo executable = serviceProvider.GetTestApplicationModuleInfo().GetCurrentExecutableInfo();
        string[] forwardedArguments = RemoveOwnedArguments([.. executable.ExpandedArguments]);
        string root = Path.Combine(serviceProvider.GetConfiguration().GetTestResultDirectory(), "Partitions", Guid.NewGuid().ToString("N"));
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var outputGate = new SemaphoreSlim(1);
        int nextPartition = 0;
        int aggregateExit = (int)ExitCode.ZeroTests;
        object verdictGate = new();
        Task[] workers = Enumerable.Range(0, Math.Min(count, Environment.ProcessorCount)).Select(_ => RunWorkerAsync()).ToArray();
        await Task.WhenAll(workers).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return aggregateExit;

        async Task RunWorkerAsync()
        {
            try
            {
                while (true)
                {
                    stop.Token.ThrowIfCancellationRequested();
                    int partition;
                    lock (verdictGate)
                    {
                        if (nextPartition == count)
                        {
                            return;
                        }

                        partition = ++nextPartition;
                    }

                    string directory = Path.Combine(root, partition.ToString(CultureInfo.InvariantCulture));
                    serviceProvider.GetFileSystem().CreateDirectory(directory);
                    List<string> arguments =
                    [
                        .. forwardedArguments,
                        "--partition", $"{partition.ToString(CultureInfo.InvariantCulture)}/{count.ToString(CultureInfo.InvariantCulture)}",
                        "--results-directory", directory,
                        "--ansi", "off", "--progress", "off",
                    ];
                    int exit = await RunChildAsync(executable, arguments, partition, count, outputGate, stop.Token).ConfigureAwait(false);
                    lock (verdictGate)
                    {
                        aggregateExit = AggregateExitCode(aggregateExit, exit);
                    }
                }
            }
            catch
            {
#if NET8_0_OR_GREATER
                await stop.CancelAsync().ConfigureAwait(false);
#else
                stop.Cancel();
#endif
                throw;
            }
        }
    }

    internal static int AggregateExitCode(int current, int next)
        => Priority(next) == Priority(current) ? Math.Min(current, next)
            : Priority(next) > Priority(current) ? next : current;

    private static int Priority(int exitCode) => (ExitCode)exitCode switch
    {
        ExitCode.ZeroTests => 0,
        ExitCode.Success => 1,
        ExitCode.MinimumExpectedTestsPolicyViolation or ExitCode.TestExecutionStoppedForMaxFailedTests
            or ExitCode.CoverageThresholdFailed or ExitCode.TestExecutionStoppedAtDeadline => 2,
        ExitCode.AtLeastOneTestFailed => 3,
        ExitCode.InvalidPlatformSetup or ExitCode.InvalidCommandLine or ExitCode.TestAdapterTestSessionFailure
            or ExitCode.IncompatibleProtocolVersion => 4,
        ExitCode.GenericFailure => 5,
        ExitCode.TestHostProcessExitedNonGracefully or ExitCode.DependentProcessExited => 6,
        ExitCode.TestSessionAborted => 8,
        _ => 7,
    };

    internal static string[] RemoveOwnedArguments(string[] arguments)
    {
        List<string> forwarded = [];
        for (int i = 0; i < arguments.Length; i++)
        {
            string argument = arguments[i];
            string option = argument.TrimStart('-');
            int separator = option.IndexOfAny(['=', ':']);
            string name = separator < 0 ? option : option.Substring(0, separator);
            if (argument.StartsWith("-", StringComparison.Ordinal)
                && !argument.StartsWith("---", StringComparison.Ordinal)
                && (name.Equals(PartitioningCommandLineOptionsProvider.PartitionCountOptionName, StringComparison.OrdinalIgnoreCase)
                    || name.Equals("results-directory", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("progress", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("ansi", StringComparison.OrdinalIgnoreCase)))
            {
                if (separator < 0)
                {
                    i++;
                }

                continue;
            }

            forwarded.Add(argument);
        }

        return [.. forwarded];
    }

    private async Task<int> RunChildAsync(
        ExecutableInfo executable, List<string> arguments, int partition, int count,
        SemaphoreSlim outputGate, CancellationToken cancellationToken)
    {
        using var responseFiles = new TemporaryResponseFiles();
        string[] originalArguments = [.. executable.Arguments];
        int responseFileIndex = Array.FindIndex(originalArguments, argument => argument.StartsWith("@", StringComparison.Ordinal));
        if (responseFileIndex >= 0)
        {
            int prefixLength = RemoveOwnedArguments(originalArguments.Take(responseFileIndex).ToArray()).Length;
            // Preserve ordering while keeping arguments the line-oriented response parser cannot
            // represent inline; other arguments may exceed the OS command-line limit.
            List<string> commandArguments = [.. arguments.Take(prefixLength)];
            List<string> segment = [];
            foreach (string argument in arguments.Skip(prefixLength))
            {
                if (argument.IndexOfAny(['"', '\r', '\n']) >= 0)
                {
                    await WriteSegmentAsync().ConfigureAwait(false);
                    commandArguments.Add(argument);
                }
                else
                {
                    segment.Add(argument);
                }
            }

            await WriteSegmentAsync().ConfigureAwait(false);
            arguments = commandArguments;

            async Task WriteSegmentAsync()
            {
                if (segment.Count == 0)
                {
                    return;
                }

                (string responseFile, FileStream stream) = responseFiles.Create();
                using (stream)
                using (var writer = new StreamWriter(stream))
                {
                    foreach (string argument in segment)
                    {
                        await writer.WriteLineAsync($"\"{argument}\"").ConfigureAwait(false);
                    }
                }

                commandArguments.Add($"@{responseFile}");
                segment.Clear();
            }
        }

        var escapedArguments = new StringBuilder();
        foreach (string argument in arguments)
        {
            PasteArguments.AppendArgument(escapedArguments, argument);
        }

        // IProcess does not expose redirected streams. Retain its cross-target wait/kill implementation.
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo(executable.FilePath, escapedArguments.ToString())
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
        };
        const string chainVariable = EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTORCHESTRATORCHAIN;
        if (RoslynString.IsNullOrEmpty(serviceProvider.GetEnvironment().GetEnvironmentVariable(chainVariable)))
        {
            process.StartInfo.EnvironmentVariables[chainVariable] = Convert.ToBase64String(Encoding.UTF8.GetBytes(Uid));
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!process.Start())
        {
            throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, PlatformResources.CannotStartProcessErrorMessage, executable.FilePath));
        }

        using SystemProcess child = new(process);
        using var childStop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var readerStop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task stdout = ForwardAsync(process.StandardOutput);
        Task stderr = ForwardAsync(process.StandardError);
        try
        {
            await child.WaitForExitAsync(childStop.Token).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                if (!child.HasExited)
                {
                    try
                    {
                        child.Kill();
                    }
                    catch (InvalidOperationException ex) when (child.HasExited)
                    {
                        await serviceProvider.GetLoggerFactory().CreateLogger<PartitioningOrchestrator>().LogDebugAsync($"Partition exited during termination: {ex}").ConfigureAwait(false);
                    }

                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                    await child.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
                }
            }
            finally
            {
#if NET8_0_OR_GREATER
                // A descendant can inherit the pipes after the direct child has exited.
                readerStop.CancelAfter(TimeSpan.FromSeconds(30));
#endif
                if (!child.HasExited)
                {
                    process.StandardOutput.Dispose();
                    process.StandardError.Dispose();
                }

                await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
            }
        }

#if NET8_0_OR_GREATER
        if (readerStop.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Timed out while draining partition output.");
        }
#endif
        return child.ExitCode;

        async Task ForwardAsync(StreamReader reader)
        {
            try
            {
#if NET8_0_OR_GREATER
                while (await reader.ReadLineAsync(readerStop.Token).ConfigureAwait(false) is { } line)
#else
                while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
#endif
                {
                    await outputGate.WaitAsync(readerStop.Token).ConfigureAwait(false);
                    try
                    {
                        string text = $"[partition {partition.ToString(CultureInfo.InvariantCulture)}/{count.ToString(CultureInfo.InvariantCulture)}] {line}";
                        IOutputDevice outputDevice = serviceProvider.GetOutputDevice();
                        if (outputDevice is ProxyOutputDevice { OriginalOutputDevice: DotnetTestPassthroughOutputDevice }
                            && serviceProvider.GetService<IPushOnlyProtocol>() is DotnetTestConnection { IsDisplayMessageForwardingSupported: false })
                        {
                            // Protocol 1.1 and 1.2 SDKs relay host console output but cannot receive DisplayMessage.
                            serviceProvider.GetConsole().WriteLine(text);
                        }
                        else
                        {
                            IOutputDeviceData data = outputDevice is ProxyOutputDevice { OriginalOutputDevice: DotnetTestPassthroughOutputDevice }
                                ? new SessionMessageOutputDeviceData(text)
                                : new TextOutputDeviceData(text);
                            await outputDevice.DisplayAsync(this, data, readerStop.Token).ConfigureAwait(false);
                        }
                    }
                    finally
                    {
                        outputGate.Release();
                    }
                }
            }
            catch (OperationCanceledException) when (readerStop.IsCancellationRequested)
            {
                // The caller handles cancellation and reports a drain timeout separately.
            }
            catch
            {
#if NET8_0_OR_GREATER
                await childStop.CancelAsync().ConfigureAwait(false);
#else
                childStop.Cancel();
#endif
                throw;
            }
        }
    }

    private sealed class TemporaryResponseFiles : IDisposable
    {
        private readonly List<string> _paths = [];

        public (string Path, FileStream Stream) Create()
        {
#if NETCOREAPP
            string path = Path.Combine(Path.GetTempPath(), $"mtp-partition-{Guid.NewGuid():N}.rsp");
            FileStream stream;
            if (OperatingSystem.IsWindows())
            {
                using var identity = WindowsIdentity.GetCurrent();
                SecurityIdentifier owner = identity.User ?? throw new InvalidOperationException("The current Windows identity has no security identifier.");
                FileSecurity security = new();
                security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
                security.AddAccessRule(new FileSystemAccessRule(owner, FileSystemRights.FullControl, AccessControlType.Allow));
                stream = FileSystemAclExtensions.Create(new FileInfo(path), FileMode.CreateNew, FileSystemRights.Read | FileSystemRights.Write,
                    FileShare.Read, 4096, FileOptions.None, security);
            }
            else
            {
                stream = new FileStream(path, new FileStreamOptions
                {
                    Mode = FileMode.CreateNew,
                    Access = FileAccess.Write,
                    Share = FileShare.Read,
                    UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite,
                });
            }

            _paths.Add(path);
            return (path, stream);
#else
            _ = _paths;
            throw new PlatformNotSupportedException("Partitioning requires a supported .NET runtime.");
#endif
        }

        public void Dispose()
        {
            List<Exception>? failures = null;
            foreach (string path in _paths)
            {
                try
                {
                    File.Delete(path);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    (failures ??= []).Add(ex);
                }
            }

            if (failures is not null)
            {
                throw new AggregateException("Unable to remove temporary partition response files.", failures);
            }
        }
    }
}
