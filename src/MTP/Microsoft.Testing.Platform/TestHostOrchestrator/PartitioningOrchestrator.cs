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
using Microsoft.Testing.Platform.Services;

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
                    int exit = await RunChildAsync(executable, arguments, directory, partition, count, outputGate, stop.Token).ConfigureAwait(false);
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
                && name is PartitioningCommandLineOptionsProvider.PartitionCountOptionName or "results-directory" or "progress" or "ansi")
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
        ExecutableInfo executable, List<string> arguments, string directory, int partition, int count,
        SemaphoreSlim outputGate, CancellationToken cancellationToken)
    {
        string[] originalArguments = [.. executable.Arguments];
        int responseFileIndex = Array.FindIndex(originalArguments, argument => argument.StartsWith("@", StringComparison.Ordinal));
        if (responseFileIndex >= 0)
        {
            int prefixLength = RemoveOwnedArguments(originalArguments.Take(responseFileIndex).ToArray()).Length;
            // Response-file expansion can exceed the OS command-line limit. Keep the muxer prefix
            // inline and recreate only the application arguments after removing our activation.
            if (!arguments.Skip(prefixLength).Any(argument => argument.IndexOf('"') >= 0))
            {
                string responseFile = Path.Combine(directory, "partition-arguments.rsp");
                using (IFileStream stream = serviceProvider.GetFileSystem().NewFileStream(responseFile, FileMode.Create, FileAccess.Write))
                using (var writer = new StreamWriter(stream.Stream))
                {
                    foreach (string argument in arguments.Skip(prefixLength))
                    {
                        await writer.WriteLineAsync($"\"{argument}\"").ConfigureAwait(false);
                    }
                }

                arguments.RemoveRange(prefixLength, arguments.Count - prefixLength);
                arguments.Add($"@{responseFile}");
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
        cancellationToken.ThrowIfCancellationRequested();
        if (!process.Start())
        {
            throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, PlatformResources.CannotStartProcessErrorMessage, executable.FilePath));
        }

        using SystemProcess child = new(process);
        using var childStop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
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
                if (!child.HasExited)
                {
                    process.StandardOutput.Dispose();
                    process.StandardError.Dispose();
                }

                await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
            }
        }

        return child.ExitCode;

        // Drain redirected streams after termination as well as normal exit; cancellation must not
        // abandon readers that still own the process pipes.
        [SuppressMessage("Reliability", "CA2016:Forward the 'cancellationToken' parameter to methods", Justification = "Readers drain until the child closes its streams.")]
        async Task ForwardAsync(StreamReader reader)
        {
            try
            {
                while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
                {
                    await outputGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
                    try
                    {
                        await serviceProvider.GetOutputDevice().DisplayAsync(
                            this, new TextOutputDeviceData($"[partition {partition.ToString(CultureInfo.InvariantCulture)}/{count.ToString(CultureInfo.InvariantCulture)}] {line}"),
                            CancellationToken.None).ConfigureAwait(false);
                    }
                    finally
                    {
                        outputGate.Release();
                    }
                }
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
}
