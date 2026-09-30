// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Microsoft.Testing.Platform.ServerMode.Client.Sources.UnitTests;

/// <summary>
/// Tests for how <see cref="MtpServerProcess"/> chooses between a sibling apphost and <c>dotnet &lt;dll&gt;</c>.
/// </summary>
/// <remarks>
/// The scenario these guard is a test payload built on a Windows agent and executed on a Linux machine. Two
/// things go wrong there: a Windows PE <c>App.exe</c> travels next to <c>App.dll</c> and is not a Linux
/// executable, and a zip round trip does not carry POSIX permission bits, so the real extensionless apphost
/// can arrive without its execute bit. Launching either one aborts the run with <c>Permission denied</c>, so
/// the launcher has to reject both and fall back to <c>dotnet &lt;dll&gt;</c>.
/// </remarks>
[TestClass]
[DoNotParallelize]
public sealed class MtpServerProcessTests
{
    // Any value works: BuildLaunch only formats the port into the argument string, it never binds it.
    private const int Port = 12345;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task StartAsyncWithCustomOptionsUsesLoggerAndPreservesLaunchDetails()
    {
        using var temp = TempDirectory.Create();
        string source = Path.Combine(temp.Path, "Missing.exe");
        var log = new List<(MtpClientLogLevel Level, string Message)>();
        var options = new MtpServerClientOptions
        {
            ConnectionTimeout = TimeSpan.FromMilliseconds(100),
            Logger = new DelegateMtpClientLogger((level, message) => log.Add((level, message))),
        };

        _ = await Assert.ThrowsExactlyAsync<Win32Exception>(
            () => MtpServerProcess.StartAsync(source, options, TestContext.CancellationToken));

        (MtpClientLogLevel Level, string Message) launch = Assert.ContainsSingle(
            entry => entry.Message.Contains("Launching MTP server", StringComparison.Ordinal),
            log);
        Assert.AreEqual(MtpClientLogLevel.Debug, launch.Level);
        Assert.Contains(source, launch.Message);
        Assert.Contains("--server --client-port", launch.Message);
        Assert.Contains("--no-banner", launch.Message);
        Assert.Contains(temp.Path, launch.Message);
    }

#if NET
    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "Uses a Windows batch file and PowerShell child process.")]
    public async Task StartAsyncTimeoutKillsProcessAndReleasesListener()
    {
        using var temp = TempDirectory.Create();
        string survivedFile = Path.Combine(temp.Path, "survived.txt");
        string source = temp.CreateFile(
            "NeverConnects.cmd",
            "@echo off\r\n"
            + "powershell.exe -NoProfile -Command \"Start-Sleep -Seconds 3\"\r\n"
            + $"echo survived>\"{survivedFile}\"\r\n");
        var log = new List<string>();
        var options = new MtpServerClientOptions
        {
            ConnectionTimeout = TimeSpan.FromMilliseconds(300),
            Logger = new DelegateMtpClientLogger((_, message) => log.Add(message)),
        };

        MtpServerConnectionClosedException exception = await Assert.ThrowsExactlyAsync<MtpServerConnectionClosedException>(
            () => MtpServerProcess.StartAsync(source, options, TestContext.CancellationToken));

        Assert.Contains("did not connect back within", exception.Message);
        string launchMessage = Assert.ContainsSingle(
            message => message.Contains("listening on port", StringComparison.Ordinal),
            log);
        Match portMatch = Regex.Match(launchMessage, @"listening on port (?<port>\d+)\.");
        Assert.IsTrue(portMatch.Success, launchMessage);
        int port = int.Parse(portMatch.Groups["port"].Value, CultureInfo.InvariantCulture);

        await Task.Delay(TimeSpan.FromSeconds(3), TestContext.CancellationToken);
        Assert.IsFalse(File.Exists(survivedFile), "The timed-out launch must kill the process before it can continue.");

        await AssertPortCanBeReboundAsync(port);
    }
#endif

    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "Uses a Windows batch file to fill redirected stdout.")]
    public async Task StartAsyncDrainsStandardOutputBeforeItCanBlockServerConnection()
    {
        using var temp = TempDirectory.Create();
        string source = temp.CreateFile(
            "WritesOutputAndConnects.cmd",
            "@echo off\r\n"
            + "set port=\r\n"
            + ":parse\r\n"
            + "if \"%~1\"==\"\" exit /b 2\r\n"
            + "if \"%~1\"==\"--client-port\" (\r\n"
            + "  set port=%~2\r\n"
            + "  goto found\r\n"
            + ")\r\n"
            + "shift\r\n"
            + "goto parse\r\n"
            + ":found\r\n"
            + "for /L %%i in (1,1,2000) do echo 01234567890123456789012345678901234567890123456789012345678901234567890123456789\r\n"
            + "powershell.exe -NoProfile -Command \"$client = [Net.Sockets.TcpClient]::new('127.0.0.1', %port%); try { Start-Sleep -Seconds 30 } finally { $client.Dispose() }\"\r\n");
        var options = new MtpServerClientOptions { ConnectionTimeout = TimeSpan.FromSeconds(15) };

        using MtpServerProcess process = await MtpServerProcess.StartAsync(source, options, TestContext.CancellationToken);
        Process launchedProcess = GetPrivateInstanceField<Process>(process, "_process");

        Assert.IsGreaterThan(0, process.ProcessId);
        Assert.IsNull(process.ExitCode);
        await process.ShutdownAsync();
        Assert.AreEqual(0, process.ProcessId);
        Assert.IsNull(process.ExitCode, "A process killed during shutdown must not expose the forced-termination exit code.");
        Assert.ThrowsExactly<ObjectDisposedException>(process.Connection.Start);
        Assert.ThrowsExactly<InvalidOperationException>(() => _ = launchedProcess.Id);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "Uses a Windows batch file as the server process.")]
    public async Task StartAsyncDoesNotCaptureSynchronizationContext()
    {
        using var temp = TempDirectory.Create();
        string source = CreateConnectingBatchFile(temp, "Connects.cmd");
        var options = new MtpServerClientOptions { ConnectionTimeout = TimeSpan.FromSeconds(10) };
        MtpServerProcess? process = null;

        await AssertDoesNotCaptureSynchronizationContextAsync(
            async () => process = await MtpServerProcess.StartAsync(source, options, TestContext.CancellationToken).ConfigureAwait(false),
            static () => { });

        Assert.IsNotNull(process);
        await process.ShutdownAsync();
    }

    [TestMethod]
    public async Task StartAsyncWhenAlreadyCanceledDoesNotAttemptToLaunch()
    {
        using var temp = TempDirectory.Create();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        string source = Path.Combine(temp.Path, "missing-directory", "App.dll");

        OperationCanceledException exception = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => MtpServerProcess.StartAsync(source, cancellationToken: cancellation.Token));

        Assert.AreEqual(cancellation.Token, exception.CancellationToken);
    }

    [TestMethod]
    public async Task StartAsyncWhenManagedAssemblyIsMissingReportsEarlyExitAndStandardError()
    {
        using var temp = TempDirectory.Create();
        string source = Path.Combine(temp.Path, "Missing.dll");
        var connectionTimeout = TimeSpan.FromSeconds(30);
        // Allow process startup and stderr-drain delays on loaded agents while retaining a ten-second gap that
        // distinguishes early-exit detection from waiting for the full connection timeout.
        var maximumExpectedDuration = TimeSpan.FromSeconds(20);
        var options = new MtpServerClientOptions { ConnectionTimeout = connectionTimeout };
        var stopwatch = Stopwatch.StartNew();

        MtpServerConnectionClosedException exception = await Assert.ThrowsExactlyAsync<MtpServerConnectionClosedException>(
            () => MtpServerProcess.StartAsync(source, options, TestContext.CancellationToken));
        stopwatch.Stop();

        Assert.Contains("exited with code", exception.Message);
        Assert.Contains("before connecting back", exception.Message);
        Assert.Contains("Standard error:", exception.Message);
        Assert.IsLessThan(
            maximumExpectedDuration,
            stopwatch.Elapsed,
            "A process that exits during startup must be reported immediately instead of waiting for the connection timeout.");
    }

    [TestMethod]
    [OSCondition(
        ConditionMode.Exclude,
        OperatingSystems.Windows,
        IgnoreMessage = "Uses the standard Unix 'false' executable to produce a silent nonzero exit.")]
    public async Task StartAsyncWhenProcessExitsWithoutStandardErrorOmitsStandardErrorSuffix()
    {
        const string FalseExecutable = "/usr/bin/false";
        var options = new MtpServerClientOptions { ConnectionTimeout = TimeSpan.FromSeconds(20) };

        MtpServerConnectionClosedException exception = await Assert.ThrowsExactlyAsync<MtpServerConnectionClosedException>(
            () => MtpServerProcess.StartAsync(FalseExecutable, options, TestContext.CancellationToken));

        Assert.Contains("exited with code", exception.Message);
        Assert.DoesNotContain("Standard error:", exception.Message);
    }

#if NET
    [TestMethod]
    [OSCondition(
        ConditionMode.Exclude,
        OperatingSystems.Windows,
        IgnoreMessage = "Uses a Unix shell script and POSIX executable permissions.")]
    [UnsupportedOSPlatform("windows")]
    public async Task StartAsyncWhenStoppedProcessHasDescendantConnectAndKeepStandardErrorOpenReportsEarlyExit()
    {
        using var temp = TempDirectory.Create();
        string script = temp.CreateFile("App");
        // A terminated process remains visible to kill -0 while it is a zombie, so the descendant waits until
        // Process.HasExited observes and reaps the launched shell before connecting and holding stderr open.
        File.WriteAllText(
            script,
            "#" + "!/bin/bash\n"
            + "while [[ \"$1\" != \"--client-port\" && \"$#\" -gt 0 ]]; do shift; done\n"
            + "port=\"$2\"\n"
            + "parent_pid=$$\n"
            + "(\n"
            + "  while kill -0 \"$parent_pid\" 2>/dev/null; do sleep 0.01; done\n"
            + "  exec 3<>\"/dev/tcp/127.0.0.1/$port\"\n"
            + "  sleep 5\n"
            + ") &\n"
            + "echo inherited-standard-error >&2\n"
            + "exit 7\n");
        MakeExecutable(script);
        var options = new MtpServerClientOptions { ConnectionTimeout = TimeSpan.FromSeconds(1) };
        var maximumExpectedDuration = TimeSpan.FromMilliseconds(4500);
        var stopwatch = Stopwatch.StartNew();

        MtpServerConnectionClosedException exception = await Assert.ThrowsExactlyAsync<MtpServerConnectionClosedException>(
            () => MtpServerProcess.StartAsync(script, options, TestContext.CancellationToken));
        stopwatch.Stop();

        Assert.Contains("exited with code 7", exception.Message);
        Assert.Contains("inherited-standard-error", exception.Message);
        Assert.DoesNotContain("did not connect back within", exception.Message);
        Assert.IsLessThan(
            maximumExpectedDuration,
            stopwatch.Elapsed,
            "The stopped-process probe includes a two-second stderr grace plus process startup and polling allowance, "
            + "but must finish before the descendant's five-second pipe hold.");
    }
#endif

    [TestMethod]
    public void BuildLaunchWhenSourceIsExeLaunchesItDirectly()
    {
        using var temp = TempDirectory.Create();
        string exe = temp.CreateFile("App.exe");

        MtpServerProcess.LaunchCommand launch = MtpServerProcess.BuildLaunch(exe, Port);

        Assert.AreEqual(exe, launch.FileName, "A native executable source must be launched directly.");
        Assert.AreEqual(temp.Path, launch.WorkingDirectory);
        Assert.AreEqual($"--server --client-port {Port} --no-banner", launch.Arguments);
    }

    [TestMethod]
    public void BuildLaunchWhenDllHasNoApphostFallsBackToDotnet()
    {
        using var temp = TempDirectory.Create();
        string dll = temp.CreateFile("App.dll");

        MtpServerProcess.LaunchCommand launch = MtpServerProcess.BuildLaunch(dll, Port);

        Assert.AreEqual("dotnet", launch.FileName, "Without an apphost the assembly must be launched by the muxer.");
        Assert.Contains($"\"{dll}\"", launch.Arguments, "The muxer needs the quoted assembly path as its first argument.");
        Assert.AreEqual(temp.Path, launch.WorkingDirectory);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "A '.exe' apphost is only launchable on Windows.")]
    public void BuildLaunchOnWindowsSelectsSiblingExeApphost()
    {
        using var temp = TempDirectory.Create();
        string dll = temp.CreateFile("App.dll");
        string exe = temp.CreateFile("App.exe");

        MtpServerProcess.LaunchCommand launch = MtpServerProcess.BuildLaunch(dll, Port);

        Assert.AreEqual(exe, launch.FileName, "On Windows the sibling '.exe' apphost is the preferred launch target.");
    }

    [TestMethod]
    [OSCondition(
        ConditionMode.Exclude,
        OperatingSystems.Windows,
        IgnoreMessage = "Asserts that a Windows PE sibling is rejected, which only matters off Windows.")]
    public void BuildLaunchOnUnixIgnoresSiblingWindowsExeAndFallsBackToDotnet()
    {
        using var temp = TempDirectory.Create();
        string dll = temp.CreateFile("App.dll");

        // The Windows apphost that rode along in the payload. It exists, but it is a Windows PE binary.
        _ = temp.CreateFile("App.exe");

        MtpServerProcess.LaunchCommand launch = MtpServerProcess.BuildLaunch(dll, Port);

        Assert.AreEqual("dotnet", launch.FileName, "A Windows '.exe' must never be selected as the apphost on Unix.");
    }

#if NET
    [TestMethod]
    [OSCondition(
        ConditionMode.Exclude,
        OperatingSystems.Windows,
        IgnoreMessage = "Unix file modes are a Unix concept.")]
    [UnsupportedOSPlatform("windows")]
    public void BuildLaunchOnUnixSelectsExecutableExtensionlessApphost()
    {
        using var temp = TempDirectory.Create();
        string dll = temp.CreateFile("App.dll");
        string apphost = temp.CreateFile("App");
        MakeExecutable(apphost);

        MtpServerProcess.LaunchCommand launch = MtpServerProcess.BuildLaunch(dll, Port);

        Assert.AreEqual(apphost, launch.FileName, "An executable extensionless apphost is the preferred launch target on Unix.");
    }

    [TestMethod]
    [OSCondition(
        ConditionMode.Exclude,
        OperatingSystems.Windows,
        IgnoreMessage = "Unix file modes are a Unix concept.")]
    [UnsupportedOSPlatform("windows")]
    public void BuildLaunchOnUnixIgnoresNonExecutableApphostAndFallsBackToDotnet()
    {
        using var temp = TempDirectory.Create();
        string dll = temp.CreateFile("App.dll");

        // The apphost survived the trip but the archive dropped its permission bits.
        string apphost = temp.CreateFile("App");
        File.SetUnixFileMode(apphost, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        MtpServerProcess.LaunchCommand launch = MtpServerProcess.BuildLaunch(dll, Port);

        Assert.AreEqual(
            "dotnet",
            launch.FileName,
            "Starting a file with no execute bit throws 'Permission denied', so the launch must fall back to the muxer.");
    }

    [TestMethod]
    [OSCondition(
        ConditionMode.Exclude,
        OperatingSystems.Windows,
        IgnoreMessage = "Unix execute permission classes do not apply on Windows.")]
    [UnsupportedOSPlatform("windows")]
    public async Task StartAsyncOnUnixWhenOnlyNonApplicableExecuteBitIsSetRetriesThroughDotnet()
    {
        using var temp = TempDirectory.Create();
        string dll = temp.CreateFile("App.dll");
        string apphost = temp.CreateFile("App");

        // The owner class applies to this process, so GroupExecute does not grant execution even when the
        // process is also a member of the file's group. The coarse mode-bit preflight accepts this candidate,
        // and Process.Start must recover from the resulting EACCES by retrying through dotnet.
        File.SetUnixFileMode(
            apphost,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupExecute);

        var log = new StringBuilder();
        var options = new MtpServerClientOptions
        {
            ConnectionTimeout = TimeSpan.FromSeconds(5),
            Logger = new DelegateMtpClientLogger((_, message) => log.AppendLine(message)),
        };

        _ = await Assert.ThrowsExactlyAsync<MtpServerConnectionClosedException>(
            () => MtpServerProcess.StartAsync(dll, options, TestContext.CancellationToken));

        Assert.Contains(
            "The sibling apphost could not be executed; retrying through 'dotnet",
            log.ToString(),
            "The EACCES failure should be recovered by retrying the managed assembly through dotnet.");
    }
#endif

    [TestMethod]
    public void IsApphostCandidateReturnsFalseWhenFileMissing()
    {
        using var temp = TempDirectory.Create();

        Assert.IsFalse(MtpServerProcess.IsApphostCandidate(Path.Combine(temp.Path, "Missing")));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "Exercises Windows short-circuit behavior in the apphost retry predicate.")]
    [DataRow("App.exe", "Apphost.exe", 13)]
    [DataRow("App.dll", "Apphost.exe", 1)]
    [DataRow("App.dll", "Apphost.exe", 13)]
    [DataRow("App.dll", "dotnet", 13)]
    public void ShouldRetryApphostThroughDotnetRejectsIncompleteRetryConditionsOnWindows(
        string source,
        string fileName,
        int nativeErrorCode)
    {
        var launch = new MtpServerProcess.LaunchCommand(fileName, string.Empty, string.Empty);
        var exception = new Win32Exception(nativeErrorCode);

        bool result = InvokePrivateStatic<bool>("ShouldRetryApphostThroughDotnet", source, launch, exception);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task TryGetProcessStoppedFailureAsyncReturnsNullForRunningProcess()
    {
        using Process process = StartLongRunningProcess();
        try
        {
            Task<Exception?> resultTask = InvokePrivateStatic<Task<Exception?>>(
                "TryGetProcessStoppedFailureAsync",
                process,
                "Running.exe",
                new StringBuilder(),
                Task.CompletedTask,
                CancellationToken.None);

            Assert.IsNull(await resultTask.ConfigureAwait(false));
        }
        finally
        {
            KillProcessIfRunning(process);
        }
    }

    [TestMethod]
    public async Task TryGetProcessStoppedFailureAsyncReturnsExitFailureForStoppedProcess()
    {
        using Process process = StartExitedProcess(17);
        var standardError = new StringBuilder("failure details");

        Task<Exception?> resultTask = InvokePrivateStatic<Task<Exception?>>(
            "TryGetProcessStoppedFailureAsync",
            process,
            "Stopped.exe",
            standardError,
            Task.CompletedTask,
            CancellationToken.None);

        MtpServerConnectionClosedException exception = Assert.IsInstanceOfType<MtpServerConnectionClosedException>(
            await resultTask.ConfigureAwait(false));
        Assert.Contains("exited with code 17", exception.Message);
        Assert.Contains("Standard error: failure details", exception.Message);
    }

    [TestMethod]
    public async Task WaitForStandardErrorAndCreateEarlyExitFailureAsyncHonorsCancellationAfterDrain()
    {
        using Process process = StartExitedProcess(3);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Task<Exception?> resultTask = InvokePrivateStatic<Task<Exception?>>(
            "WaitForStandardErrorAndCreateEarlyExitFailureAsync",
            process,
            "Stopped.exe",
            new StringBuilder(),
            Task.CompletedTask,
            cancellation.Token);

        OperationCanceledException exception = await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => resultTask);
        Assert.AreEqual(cancellation.Token, exception.CancellationToken);
    }

    [TestMethod]
    public async Task WaitForStandardErrorAndCreateEarlyExitFailureAsyncDoesNotCaptureSynchronizationContext()
    {
        using Process process = StartExitedProcess(4);
        var drained = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<Exception?>? resultTask = null;

        await AssertDoesNotCaptureSynchronizationContextAsync(
            () =>
            {
                resultTask = InvokePrivateStatic<Task<Exception?>>(
                    "WaitForStandardErrorAndCreateEarlyExitFailureAsync",
                    process,
                    "Stopped.exe",
                    new StringBuilder(),
                    drained.Task,
                    CancellationToken.None);
                return resultTask;
            },
            () => drained.SetResult(null));

        Assert.IsInstanceOfType<MtpServerConnectionClosedException>(await resultTask!.ConfigureAwait(false));
    }

    [TestMethod]
    public async Task CreateTimeoutOrStoppedFailureAsyncReturnsStoppedFailureWhenProcessExited()
    {
        using Process process = StartExitedProcess(19);

        Task<Exception> resultTask = InvokePrivateStatic<Task<Exception>>(
            "CreateTimeoutOrStoppedFailureAsync",
            process,
            "Stopped.exe",
            new StringBuilder("last error"),
            Task.CompletedTask,
            TimeSpan.FromSeconds(12),
            CancellationToken.None);

        MtpServerConnectionClosedException exception = Assert.IsInstanceOfType<MtpServerConnectionClosedException>(
            await resultTask.ConfigureAwait(false));
        Assert.Contains("exited with code 19", exception.Message);
        Assert.Contains("Standard error: last error", exception.Message);
        Assert.DoesNotContain("did not connect back within", exception.Message);
    }

    [TestMethod]
    public async Task CreateTimeoutOrStoppedFailureAsyncHonorsCancellationForRunningProcess()
    {
        using Process process = StartLongRunningProcess();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        try
        {
            Task<Exception> resultTask = InvokePrivateStatic<Task<Exception>>(
                "CreateTimeoutOrStoppedFailureAsync",
                process,
                "Running.exe",
                new StringBuilder(),
                Task.CompletedTask,
                TimeSpan.FromSeconds(12),
                cancellation.Token);

            OperationCanceledException exception = await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => resultTask);
            Assert.AreEqual(cancellation.Token, exception.CancellationToken);
        }
        finally
        {
            KillProcessIfRunning(process);
        }
    }

    [TestMethod]
    public async Task CreateTimeoutOrStoppedFailureAsyncDoesNotCaptureSynchronizationContext()
    {
        using Process process = StartExitedProcess(21);
        var drained = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<Exception>? resultTask = null;

        await AssertDoesNotCaptureSynchronizationContextAsync(
            () =>
            {
                resultTask = InvokePrivateStatic<Task<Exception>>(
                    "CreateTimeoutOrStoppedFailureAsync",
                    process,
                    "Stopped.exe",
                    new StringBuilder(),
                    drained.Task,
                    TimeSpan.FromSeconds(12),
                    CancellationToken.None);
                return resultTask;
            },
            () => drained.SetResult(null));

        MtpServerConnectionClosedException exception = Assert.IsInstanceOfType<MtpServerConnectionClosedException>(
            await resultTask!.ConfigureAwait(false));
        Assert.Contains("exited with code 21", exception.Message);
    }

    [TestMethod]
    public void GetStandardErrorReturnsEmptyStringForWhitespaceOnlyBuffer()
    {
        string result = InvokePrivateStatic<string>("GetStandardError", new StringBuilder(" \r\n\t "));

        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void GetStandardErrorTrimsAndPrefixesNonEmptyBuffer()
    {
        string result = InvokePrivateStatic<string>("GetStandardError", new StringBuilder(" \r\n details \r\n "));

        Assert.AreEqual("Standard error: details", result);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "Uses a Windows batch file to produce redirected stderr.")]
    public async Task CreateProcessEnablesEventsAndSignalsCleanStandardErrorDrain()
    {
        using var temp = TempDirectory.Create();
        string source = temp.CreateFile("NoStandardError.cmd", "@echo off\r\nexit /b 0\r\n");
        var startInfo = new ProcessStartInfo
        {
            FileName = source,
            WorkingDirectory = temp.Path,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };
        var standardError = new StringBuilder();
        object?[] arguments = [startInfo, standardError, null];

        using Process process = InvokePrivateStatic<Process>("CreateProcess", arguments);
        Task standardErrorDrained = Assert.IsInstanceOfType<Task>(arguments[2]);

        Assert.IsTrue(process.EnableRaisingEvents);
        process.Start();
        process.BeginErrorReadLine();
        process.BeginOutputReadLine();
        await Task.Run(process.WaitForExit, TestContext.CancellationToken);

        Task completed = await Task.WhenAny(standardErrorDrained, Task.Delay(TimeSpan.FromSeconds(5), TestContext.CancellationToken)).ConfigureAwait(false);
        Assert.AreSame(standardErrorDrained, completed);
        Assert.IsTrue(Assert.IsInstanceOfType<Task<bool>>(standardErrorDrained).Result);
        Assert.AreEqual(0, standardError.Length, "The end-of-stream callback must return after signaling the drain.");
    }

    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "Uses a Windows batch file to produce redirected stderr.")]
    public async Task CreateProcessCapturesShortStandardErrorLine()
    {
        using var temp = TempDirectory.Create();
        string source = temp.CreateFile("WritesStandardError.cmd", "@echo off\r\necho expected-error 1>&2\r\n");
        var startInfo = new ProcessStartInfo
        {
            FileName = source,
            WorkingDirectory = temp.Path,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };
        var standardError = new StringBuilder();
        object?[] arguments = [startInfo, standardError, null];

        using Process process = InvokePrivateStatic<Process>("CreateProcess", arguments);
        Task standardErrorDrained = Assert.IsInstanceOfType<Task>(arguments[2]);
        process.Start();
        process.BeginErrorReadLine();
        process.BeginOutputReadLine();
        await Task.Run(process.WaitForExit, TestContext.CancellationToken);
        await standardErrorDrained.ConfigureAwait(false);

        Assert.AreEqual("expected-error", standardError.ToString().Trim());
    }

    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "Uses a Windows batch file to produce redirected stderr.")]
    public async Task CreateProcessRetainsOnlyMaximumStandardErrorTail()
    {
        using var temp = TempDirectory.Create();
        string source = temp.CreateFile(
            "WritesLargeStandardError.cmd",
            "@echo off\r\n"
            + "powershell.exe -NoProfile -Command \"[Console]::Error.WriteLine('x' * 70000)\"\r\n");
        var startInfo = new ProcessStartInfo
        {
            FileName = source,
            WorkingDirectory = temp.Path,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };
        var standardError = new StringBuilder();
        object?[] arguments = [startInfo, standardError, null];

        using Process process = InvokePrivateStatic<Process>("CreateProcess", arguments);
        Task standardErrorDrained = Assert.IsInstanceOfType<Task>(arguments[2]);
        process.Start();
        process.BeginErrorReadLine();
        process.BeginOutputReadLine();
        await Task.Run(process.WaitForExit, TestContext.CancellationToken);
        await standardErrorDrained.ConfigureAwait(false);

        Assert.AreEqual(64 * 1024, standardError.Length);
        Assert.EndsWith(Environment.NewLine, standardError.ToString());
        Assert.IsTrue(standardError.ToString().TrimEnd().All(character => character == 'x'));
    }

    [TestMethod]
    public void SafeKillReturnsFalseForProcessThatAlreadyExited()
    {
        using Process process = StartExitedProcess(0);

        bool killed = InvokePrivateStatic<bool>("SafeKill", process, NullMtpClientLogger.Instance);

        Assert.IsFalse(killed);
    }

    [TestMethod]
    public void SafeKillKillsRunningProcessAndReturnsTrue()
    {
        using Process process = StartLongRunningProcess();
        var log = new List<string>();
        try
        {
            bool killed = InvokePrivateStatic<bool>(
                "SafeKill",
                process,
                new DelegateMtpClientLogger((_, message) => log.Add(message)));

            Assert.IsTrue(killed);
            Assert.IsTrue(process.HasExited);
            Assert.IsEmpty(log);
        }
        finally
        {
            KillProcessIfRunning(process);
        }
    }

    [TestMethod]
    public void SafeKillLogsFailureForUnstartedProcess()
    {
        using var process = new Process();
        var log = new List<(MtpClientLogLevel Level, string Message)>();

        bool killed = InvokePrivateStatic<bool>(
            "SafeKill",
            process,
            new DelegateMtpClientLogger((level, message) => log.Add((level, message))));

        Assert.IsFalse(killed);
        (MtpClientLogLevel Level, string Message) entry = Assert.ContainsSingle(log);
        Assert.AreEqual(MtpClientLogLevel.Debug, entry.Level);
        Assert.Contains("Killing the MTP server process threw:", entry.Message);
        Assert.Contains(nameof(InvalidOperationException), entry.Message);
    }

#if NET
    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "Uses a Windows child process to distinguish whole-tree termination.")]
    public async Task SafeKillTerminatesEntireProcessTree()
    {
        using var temp = TempDirectory.Create();
        string survivedFile = Path.Combine(temp.Path, "child-survived.txt");
        string source = temp.CreateFile(
            "ProcessTree.cmd",
            "@echo off\r\n"
            + $"start \"\" /min powershell.exe -NoProfile -Command \"Start-Sleep -Seconds 2; Set-Content -Path '{survivedFile}' -Value survived\"\r\n"
            + "ping 127.0.0.1 -n 31 > nul\r\n");
        using Process process = Process.Start(new ProcessStartInfo
        {
            FileName = source,
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;

        try
        {
            bool killed = InvokePrivateStatic<bool>("SafeKill", process, NullMtpClientLogger.Instance);

            Assert.IsTrue(killed);
            await Task.Delay(TimeSpan.FromSeconds(4), TestContext.CancellationToken);
            Assert.IsFalse(File.Exists(survivedFile), "Whole-tree termination must not leave the child process running.");
        }
        finally
        {
            KillProcessIfRunning(process);
        }
    }
#endif

    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "Uses a Windows batch file as the server process.")]
    public async Task ShutdownPreservesNaturalExitCodeAfterDisposingProcess()
    {
        using var temp = TempDirectory.Create();
        string source = temp.CreateFile(
            "ConnectsThenExits.cmd",
            "@echo off\r\n"
            + "set port=\r\n"
            + ":parse\r\n"
            + "if \"%~1\"==\"\" exit /b 2\r\n"
            + "if \"%~1\"==\"--client-port\" (\r\n"
            + "  set port=%~2\r\n"
            + "  goto found\r\n"
            + ")\r\n"
            + "shift\r\n"
            + "goto parse\r\n"
            + ":found\r\n"
            + "powershell.exe -NoProfile -Command \"$client = [Net.Sockets.TcpClient]::new('127.0.0.1', %port%); Start-Sleep -Seconds 2; $client.Dispose(); exit 23\"\r\n"
            + "exit /b %errorlevel%\r\n");
        var options = new MtpServerClientOptions { ConnectionTimeout = TimeSpan.FromSeconds(10) };

        using MtpServerProcess process = await MtpServerProcess.StartAsync(source, options, TestContext.CancellationToken);
        Assert.IsGreaterThan(0, process.ProcessId);
        Assert.IsNull(process.ExitCode);
        Assert.IsTrue(
            SpinWait.SpinUntil(() => process.ExitCode == 23, TimeSpan.FromSeconds(10)),
            "The server process did not exit naturally with the expected code.");
        Assert.AreEqual(0, process.ProcessId);

        await process.ShutdownAsync();

        Assert.AreEqual(23, process.ExitCode);
    }

#if NET
    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "Uses repeated Windows process launches and handle counts.")]
    public async Task FailedLaunchDisposesProcessHandles()
    {
        using var temp = TempDirectory.Create();
        string source = temp.CreateFile(
            "NeverConnects.cmd",
            "@echo off\r\n"
            + "ping 127.0.0.1 -n 31 > nul\r\n");
        var options = new MtpServerClientOptions { ConnectionTimeout = TimeSpan.FromMilliseconds(50) };

        _ = await Assert.ThrowsExactlyAsync<MtpServerConnectionClosedException>(
            () => MtpServerProcess.StartAsync(source, options, TestContext.CancellationToken));
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        using var currentProcess = Process.GetCurrentProcess();
        int baselineHandleCount = currentProcess.HandleCount;
        for (int i = 0; i < 12; i++)
        {
            _ = await Assert.ThrowsExactlyAsync<MtpServerConnectionClosedException>(
                () => MtpServerProcess.StartAsync(source, options, TestContext.CancellationToken));
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        currentProcess.Refresh();
        int leakedHandleCount = currentProcess.HandleCount - baselineHandleCount;
        Assert.IsLessThan(12, leakedHandleCount, $"Failed launches leaked {leakedHandleCount} process handles.");
    }
#endif

    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "Windows has no execute bit, so this asserts the Windows-only branch.")]
    public void IsApphostCandidateOnWindowsReturnsTrueForExistingFile()
    {
        using var temp = TempDirectory.Create();
        string apphost = temp.CreateFile("App.exe");

        Assert.IsTrue(MtpServerProcess.IsApphostCandidate(apphost), "Windows has no execute bit, so existence is the whole check there.");
    }

#if NET
    [TestMethod]
    [OSCondition(
        ConditionMode.Exclude,
        OperatingSystems.Windows,
        IgnoreMessage = "Unix file modes are a Unix concept.")]
    [UnsupportedOSPlatform("windows")]
    public void IsApphostCandidateOnUnixReturnsFalseForNonExecutableFile()
    {
        using var temp = TempDirectory.Create();
        string apphost = temp.CreateFile("App");
        File.SetUnixFileMode(apphost, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        Assert.IsFalse(MtpServerProcess.IsApphostCandidate(apphost));
    }

    [TestMethod]
    [OSCondition(
        ConditionMode.Exclude,
        OperatingSystems.Windows,
        IgnoreMessage = "Unix file modes are a Unix concept.")]
    [UnsupportedOSPlatform("windows")]
    public void IsApphostCandidateOnUnixReturnsTrueForExecutableFile()
    {
        using var temp = TempDirectory.Create();
        string apphost = temp.CreateFile("App");
        MakeExecutable(apphost);

        Assert.IsTrue(MtpServerProcess.IsApphostCandidate(apphost));
    }

    /// <summary>
    /// Grants the owner execute permission only, so the test also proves the check does not require all three
    /// execute bits.
    /// </summary>
    [UnsupportedOSPlatform("windows")]
    private static void MakeExecutable(string path)
        => File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
#endif

    private sealed class TempDirectory : IDisposable
    {
        private TempDirectory(string path) => Path = path;

        public string Path { get; }

        public static TempDirectory Create()
        {
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"mtp-apphost-{Guid.NewGuid():N}");
            _ = Directory.CreateDirectory(path);
            return new TempDirectory(path);
        }

        /// <summary>
        /// Creates an empty file in the directory and returns its full path. The content is irrelevant: the
        /// launcher only looks at the path, its existence, and its permissions.
        /// </summary>
        public string CreateFile(string fileName, string content = "")
        {
            string fullPath = System.IO.Path.Combine(Path, fileName);
            File.WriteAllText(fullPath, content);
            return fullPath;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort cleanup: a temp directory left behind must never fail a test.
            }
        }
    }

    private static string CreateConnectingBatchFile(TempDirectory temp, string fileName)
        => temp.CreateFile(
            fileName,
            "@echo off\r\n"
            + "set port=\r\n"
            + ":parse\r\n"
            + "if \"%~1\"==\"\" exit /b 2\r\n"
            + "if \"%~1\"==\"--client-port\" (\r\n"
            + "  set port=%~2\r\n"
            + "  goto found\r\n"
            + ")\r\n"
            + "shift\r\n"
            + "goto parse\r\n"
            + ":found\r\n"
            + "powershell.exe -NoProfile -Command \"$client = [Net.Sockets.TcpClient]::new('127.0.0.1', %port%); try { Start-Sleep -Seconds 30 } finally { $client.Dispose() }\"\r\n");

    private static void KillProcessIfRunning(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
#if NET
                process.Kill(entireProcessTree: true);
#else
                process.Kill();
#endif
                process.WaitForExit();
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static Process StartExitedProcess(int exitCode)
    {
        Process process = Environment.OSVersion.Platform == PlatformID.Win32NT
            ? Process.Start(new ProcessStartInfo
            {
                FileName = Environment.GetEnvironmentVariable("COMSPEC") ?? "cmd.exe",
                Arguments = $"/d /c exit {exitCode}",
                UseShellExecute = false,
            })!
            : Process.Start(new ProcessStartInfo
            {
                FileName = "/bin/sh",
                Arguments = $"-c \"exit {exitCode}\"",
                UseShellExecute = false,
            })!;
        process.WaitForExit();
        return process;
    }

    private static Process StartLongRunningProcess() => Environment.OSVersion.Platform == PlatformID.Win32NT
            ? Process.Start(new ProcessStartInfo
            {
                FileName = Environment.GetEnvironmentVariable("COMSPEC") ?? "cmd.exe",
                Arguments = "/d /c ping 127.0.0.1 -n 31 > nul",
                UseShellExecute = false,
                CreateNoWindow = true,
            })!
            : Process.Start(new ProcessStartInfo
            {
                FileName = "/bin/sh",
                Arguments = "-c \"sleep 30\"",
                UseShellExecute = false,
            })!;

    private static T InvokePrivateStatic<T>(string methodName, params object?[] arguments)
    {
        MethodInfo method = typeof(MtpServerProcess)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(candidate =>
            {
                if (candidate.Name != methodName)
                {
                    return false;
                }

                ParameterInfo[] parameters = candidate.GetParameters();
                if (parameters.Length != arguments.Length)
                {
                    return false;
                }

                for (int i = 0; i < parameters.Length; i++)
                {
                    if (parameters[i].IsOut || arguments[i] is null)
                    {
                        continue;
                    }

                    if (!parameters[i].ParameterType.IsInstanceOfType(arguments[i]))
                    {
                        return false;
                    }
                }

                return true;
            });

        try
        {
            return (T)method.Invoke(null, arguments)!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }

    private static T GetPrivateInstanceField<T>(object instance, string fieldName)
        => (T)(instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)
            ?? throw new InvalidOperationException($"Field '{fieldName}' is null."));

#if NET
    private async Task AssertPortCanBeReboundAsync(int port)
    {
        var stopwatch = Stopwatch.StartNew();
        SocketException? lastException = null;
        while (stopwatch.Elapsed < TimeSpan.FromSeconds(5))
        {
            var replacement = new TcpListener(IPAddress.Loopback, port);
            try
            {
                replacement.Start();
                return;
            }
            catch (SocketException ex)
            {
                lastException = ex;
                await Task.Delay(50, TestContext.CancellationToken);
            }
            finally
            {
                replacement.Stop();
            }
        }

        Assert.Fail($"Port {port} could not be rebound after server shutdown: {lastException}");
    }
#endif

    private async Task AssertDoesNotCaptureSynchronizationContextAsync(Func<Task> startOperation, Action releaseOperation)
    {
        var context = new RecordingSynchronizationContext();
        SynchronizationContext? previous = SynchronizationContext.Current;
        Task operation;
        try
        {
            SynchronizationContext.SetSynchronizationContext(context);
            operation = startOperation();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        releaseOperation();
        Task completed = await Task.WhenAny(
            operation,
            Task.Delay(TimeSpan.FromSeconds(20), TestContext.CancellationToken)).ConfigureAwait(false);

        Assert.AreSame(operation, completed, "The operation did not complete within the expected timeout.");
        await operation.ConfigureAwait(false);
        Assert.AreEqual(0, context.PostCount);
    }

    private sealed class RecordingSynchronizationContext : SynchronizationContext
    {
        private int _postCount;

        public int PostCount => _postCount;

        public override void Post(SendOrPostCallback d, object? state)
        {
            Interlocked.Increment(ref _postCount);
            ThreadPool.QueueUserWorkItem(_ => d(state));
        }
    }
}
