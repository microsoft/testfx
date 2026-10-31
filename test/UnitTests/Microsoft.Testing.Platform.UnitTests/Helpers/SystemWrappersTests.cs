// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Hosts;
using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
[UnsupportedOSPlatform("browser")]
#if !NETFRAMEWORK
[UnsupportedOSPlatform("wasi")]
[UnsupportedOSPlatform("ios")]
[UnsupportedOSPlatform("tvos")]
#endif
public sealed class SystemWrappersTests
{
    private static readonly TimeSpan ChildLimit = TimeSpan.FromSeconds(30);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task FileSystem_MoveReplaceCopyAndStreamsPreserveExactContentsAndEnumeration()
    {
        string root = Path.Combine(Path.GetTempPath(), "core-files-" + Guid.NewGuid().ToString("N"));
        SystemFileSystem files = new();
        try
        {
            Assert.AreEqual(Path.GetFullPath(root), files.CreateDirectory(root));
            Assert.IsTrue(files.ExistDirectory(root));
            string first = Path.Combine(root, "first.txt");
            using (IFileStream stream = files.NewFileStream(first, FileMode.CreateNew))
            {
                Assert.AreEqual(first, stream.Name);
                byte[] content = Encoding.UTF8.GetBytes("first payload");
                stream.Stream.Write(content, 0, content.Length);
            }

            Assert.AreEqual("first payload", files.ReadAllText(first));
            Assert.AreEqual("first payload", await files.ReadAllTextAsync(first));
            string copied = Path.Combine(root, "copied.txt");
            files.CopyFile(first, copied);
            Assert.AreEqual("first payload", files.ReadAllText(copied));
            Assert.ThrowsExactly<IOException>(() => files.CopyFile(first, copied));
            using (IFileStream stream = files.NewFileStream(first, FileMode.Truncate, FileAccess.Write, FileShare.Read))
            {
                byte[] content = Encoding.UTF8.GetBytes("replacement payload");
                stream.Stream.Write(content, 0, content.Length);
            }

            files.CopyFile(first, copied, overwrite: true);
            Assert.AreEqual("replacement payload", files.ReadAllText(copied));
            string moved = Path.Combine(root, "moved.txt");
            files.MoveFile(copied, moved);
            Assert.IsFalse(files.ExistFile(copied));
            Assert.AreEqual("replacement payload", files.ReadAllText(moved));
            Assert.ThrowsExactly<IOException>(() => files.MoveFile(first, moved));
            files.MoveFile(first, moved, overwrite: true);
            Assert.IsFalse(files.ExistFile(first));
            using (IFileStream stream = files.NewFileStream(first, FileMode.CreateNew, FileAccess.Write))
            {
                byte[] content = Encoding.UTF8.GetBytes("final payload");
                stream.Stream.Write(content, 0, content.Length);
            }

            files.ReplaceFile(first, moved);
            Assert.IsFalse(files.ExistFile(first));
            Assert.AreEqual("final payload", files.ReadAllText(moved));
            string absent = Path.Combine(root, "absent.txt");
            files.ReplaceFile(moved, absent);
            Assert.IsFalse(files.ExistFile(moved));
            Assert.AreEqual("final payload", files.ReadAllText(absent));
            Assert.AreSequenceEqual([absent], files.GetFiles(root, "*.txt", SearchOption.AllDirectories));
            Assert.IsEmpty(files.GetFiles(root, "*.missing", SearchOption.TopDirectoryOnly));
#if NETCOREAPP
            await using (SystemFileStream stream = new(absent, FileMode.Open, FileAccess.Read))
            {
                Assert.AreEqual(absent, stream.Name);
                Assert.AreEqual(102, stream.Stream.ReadByte());
            }
#endif
            files.DeleteFile(absent);
            files.DeleteFile(absent);
            Assert.IsFalse(files.ExistFile(absent));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    public void Environment_ReadsAndUpdatesDedicatedVariableWithoutClobberingOtherState()
    {
        SystemEnvironment environment = new();
        string key = "TESTFX_CORE_VARIABLE_" + Guid.NewGuid().ToString("N");
        try
        {
            environment.SetEnvironmentVariable(key, "first");
            Assert.AreEqual("first", environment.GetEnvironmentVariable(key));
            Assert.AreEqual("first", environment.GetEnvironmentVariables()[key]);
            environment.SetEnvironmentVariable(key, "second");
            Assert.AreEqual("second", environment.GetEnvironmentVariable(key));
            environment.SetEnvironmentVariable(key, null);
            Assert.IsNull(environment.GetEnvironmentVariable(key));
            Assert.AreEqual(Environment.CommandLine, environment.CommandLine);
            Assert.AreEqual(Environment.MachineName, environment.MachineName);
            Assert.AreEqual(Environment.NewLine, environment.NewLine);
            Assert.AreEqual(Environment.OSVersion.ToString(), environment.OsVersion);
            Assert.AreSequenceEqual(Environment.GetCommandLineArgs(), environment.GetCommandLineArgs());
            using var current = Process.GetCurrentProcess();
            Assert.AreEqual(current.Id, environment.ProcessId);
            Assert.AreEqual(current.Id, environment.ProcessId);
            Assert.AreEqual(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify),
                environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify));
#if NETCOREAPP
            Assert.AreEqual(Environment.ProcessPath, environment.ProcessPath);
#endif
        }
        finally
        {
            environment.SetEnvironmentVariable(key, null);
        }
    }

    [TestMethod]
    [DataRow("exit")]
    [DataRow("fail-fast")]
    [DataRow("fail-fast-exception")]
    public async Task Environment_ExitAndFailFastAreConfinedToOwnedChild(string mode)
    {
        ChildResult child = await RunChildAsync(mode);

        if (mode == "exit")
        {
            Assert.AreEqual(37, child.ExitCode);
        }
        else
        {
            Assert.AreNotEqual(0, child.ExitCode);
#if NETFRAMEWORK
            Assert.AreEqual(unchecked((int)0x80131623), child.ExitCode, "The CLR's FailFast HRESULT distinguishes this from an unrelated startup crash.");
            Assert.AreEqual(mode == "fail-fast" ? "FAIL_FAST_WITHOUT_EXCEPTION" : "FAIL_FAST_WITH_EXCEPTION", child.Output.Trim());
#else
            Assert.Contains("core-wrapper-fail-fast", child.Error);
            if (mode == "fail-fast-exception")
            {
                Assert.Contains("PROBE_EXCEPTION", child.Error);
            }
#endif
        }
    }

    [TestMethod]
    public async Task ProcessWrappers_OwnedBlockedChildForwardsIdentityExitEventAndAllWaitShapes()
    {
        using Process child = new() { StartInfo = CreateChildStartInfo("block"), EnableRaisingEvents = true };
        Assert.IsTrue(child.Start());
        using SystemProcess process = new(child);
        try
        {
#if NETCOREAPP
            Task<string?> ready = child.StandardOutput.ReadLineAsync(TestContext.CancellationToken).AsTask();
#else
            Task<string?> ready = child.StandardOutput.ReadLineAsync();
#endif
            await ready.TimeoutAfterAsync(ChildLimit);
            Assert.AreEqual("READY", await ready);
            Assert.IsFalse(process.HasExited);
            Assert.AreEqual(child.Id, process.Id);
            Assert.AreEqual(child.ProcessName, process.Name);
            Assert.AreEqual(child.StartTime, process.StartTime);
            Assert.AreEqual(child.MainModule!.FileName, process.MainModule!.FileName);
#if NETCOREAPP
            Assert.IsNull(new SystemMainModule(null).FileName);
#endif
            CancellationToken canceled = new(canceled: true);
            OperationCanceledException cancellation = await Assert.ThrowsAsync<OperationCanceledException>(() => process.WaitForExitAsync(canceled));
            Assert.AreEqual(canceled, cancellation.CancellationToken);
            TaskCompletionSource<object?> exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
            process.Exited += (sender, _) => exited.TrySetResult(sender);

            process.Kill();
            Func<Task> waitWithoutToken = process.WaitForExitAsync;
            await waitWithoutToken().TimeoutAfterAsync(ChildLimit);
            await process.WaitForExitAsync(CancellationToken.None).TimeoutAfterAsync(ChildLimit);
            process.WaitForExit();

            await exited.Task.TimeoutAfterAsync(ChildLimit);
            Assert.AreSame(child, await exited.Task);
            Assert.IsTrue(process.HasExited);
            Assert.AreEqual(child.ExitCode, process.ExitCode);
            Assert.AreNotEqual(0, process.ExitCode);
        }
        finally
        {
            if (!child.HasExited)
            {
                child.Kill();
                await child.WaitForExitAsync(CancellationToken.None).TimeoutAfterAsync(ChildLimit);
            }
        }
    }

    [TestMethod]
    public async Task ProcessHandler_StartAndLookupPreserveOwnedChildExitCode()
    {
        SystemProcessHandler handler = new();
        using IProcess current = handler.GetCurrentProcess();
        using IProcess same = handler.GetProcessById(current.Id);
        Assert.AreEqual(current.Id, same.Id);
        Assert.AreEqual(current.Name, same.Name);
        using IProcess child = handler.Start(CreateChildStartInfo("exit"));
        try
        {
            await child.WaitForExitAsync(TestContext.CancellationToken).TimeoutAfterAsync(ChildLimit);
            Assert.IsTrue(child.HasExited);
            Assert.AreEqual(37, child.ExitCode);
        }
        finally
        {
            if (!child.HasExited)
            {
                child.Kill();
                Func<Task> finishTermination = child.WaitForExitAsync;
                await finishTermination().TimeoutAfterAsync(ChildLimit);
            }
        }

        Assert.ThrowsExactly<System.ComponentModel.Win32Exception>(() => handler.Start(new ProcessStartInfo
        {
            FileName = Path.Combine(Path.GetTempPath(), "missing-process-" + Guid.NewGuid().ToString("N")),
            UseShellExecute = false,
        }));
    }

    [TestMethod]
    public async Task SystemTask_RunDelayAndWhenAllPreserveValuesCancellationAndFailures()
    {
        SystemTask task = new();
        int called = 0;
        await task.Run(() => Interlocked.Increment(ref called));
        await task.Run(
            () =>
            {
                called++;
                return Task.CompletedTask;
            },
            CancellationToken.None);
        Assert.AreEqual(17, await task.Run(() => Task.FromResult(17), CancellationToken.None));
        Assert.AreEqual(2, called);
        CancellationToken token = new(canceled: true);
        OperationCanceledException canceled = await Assert.ThrowsAsync<OperationCanceledException>(
            () => task.Run(
                () =>
                {
                    called++;
                    return Task.FromResult(99);
                },
                token));
        Assert.AreEqual(token, canceled.CancellationToken);
        Assert.AreEqual(2, called);
        await task.Delay(0);
        OperationCanceledException delayCanceled = await Assert.ThrowsAsync<OperationCanceledException>(() => task.Delay(Timeout.InfiniteTimeSpan, token));
        Assert.AreEqual(token, delayCanceled.CancellationToken);
        IOException expected = new("scheduled failure");
        Assert.AreSame(expected, await Assert.ThrowsExactlyAsync<IOException>(() => task.WhenAll(Task.CompletedTask, Task.FromException(expected))));
    }

    [TestMethod]
    public async Task SystemTask_LongRunningNamesBackgroundThreadAndHonorsPreCancellation()
    {
        SystemTask task = new();
        string? name = null;
        bool background = false;
        await task.RunLongRunning(
            () =>
            {
                name = Thread.CurrentThread.Name;
                background = Thread.CurrentThread.IsBackground;
                return Task.CompletedTask;
            },
            "core-wrapper-worker",
            CancellationToken.None);

        Assert.AreEqual("core-wrapper-worker", name);
        Assert.IsTrue(background);
        int canceledCalls = 0;
        await task.RunLongRunning(
            () =>
            {
                canceledCalls++;
                return Task.CompletedTask;
            },
            "must-not-start",
            new CancellationToken(canceled: true));
        Assert.AreEqual(0, canceledCalls);
        SystemRuntimeFeature runtime = new();
        Assert.IsFalse(runtime.IsHotReloadEnabled);
        runtime.EnableHotReload();
        runtime.EnableHotReload();
        Assert.IsTrue(runtime.IsHotReloadEnabled);
        Assert.IsFalse(new SystemRuntimeFeature().IsHotReloadEnabled);
#if NETCOREAPP
        Assert.AreEqual(RuntimeFeature.IsDynamicCodeSupported, runtime.IsDynamicCodeSupported);
#else
        Assert.IsTrue(runtime.IsDynamicCodeSupported);
#endif
    }

    [TestMethod]
    public async Task SystemTask_LongRunningFailureFaultsOwnedChildRatherThanTestHost()
    {
        ChildResult child = await RunChildAsync("long-running-failure");

        Assert.AreNotEqual(0, child.ExitCode);
#if NETFRAMEWORK
        Assert.Contains("probe-sentinel", child.Error);
#else
        Assert.Contains("PROBE_EXCEPTION", child.Error);
#endif
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task UnhandledExceptionHandler_RealChildEventsPreserveWorkflowConsoleAndCriticalLog(bool taskException, bool testHost)
    {
        ChildResult child = await RunChildAsync($"handler-{(taskException ? "task" : "thread")}-{(testHost ? "host" : "controller")}");
        Assert.AreNotEqual(0, child.ExitCode);
        string workflow = testHost ? "(testhost workflow)" : "(testhost controller workflow)";
        string prefix = taskException
            ? "[UnhandledExceptionHandler.OnTaskSchedulerUnobservedTaskException" + workflow + "]"
            : "[UnhandledExceptionHandler.OnCurrentDomainUnhandledException" + workflow + "]";
        string exception = taskException
            ? new AggregateException(
                "A Task's exception(s) were not observed either by Waiting on the Task or accessing its Exception property. As a result, the unobserved exception was rethrown by the finalizer thread.",
                new SystemWrapperChildProbe.ProbeException()).ToString()
            : "PROBE_EXCEPTION";
        string consoleMessage = taskException
            ? prefix + " Unhandled exception: " + exception
            : prefix + " " + exception + Environment.NewLine + "IsTerminating: True";
        string logMessage = taskException ? prefix + " Unhandled task exception" : prefix + " IsTerminating: True";
        string[] records = child.Output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimStart('\uFEFF'))
            .Where(line => line.StartsWith("PROBE|", StringComparison.Ordinal))
            .Select(line => Encoding.UTF8.GetString(Convert.FromBase64String(line["PROBE|".Length..])))
            .ToArray();
        string[] expected = taskException
            ? [
                "console:" + consoleMessage,
                "log-Critical:" + logMessage + Environment.NewLine + "------Exception detail------" + Environment.NewLine + exception,
                "fail-fast-exception:System.AggregateException",
            ]
            : [
                "console:" + consoleMessage,
                "log-Critical:" + logMessage + Environment.NewLine + "------Exception detail------" + Environment.NewLine + exception,
            ];
        Assert.AreSequenceEqual(expected, records);
        if (taskException)
        {
            Assert.AreEqual(44, child.ExitCode);
        }
    }

    private static ProcessStartInfo CreateChildStartInfo(string mode, string? moduleOverride = null)
    {
        string module = typeof(SystemWrappersTests).Assembly.Location;
#if NETCOREAPP
        string host = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(module)!, "..", "..", "..", "..", "..", ".dotnet",
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dotnet.exe" : "dotnet"));
        ProcessStartInfo start = new(host);
        start.ArgumentList.Add(moduleOverride ?? module);
#else
        ProcessStartInfo start = new(moduleOverride ?? module);
#endif
        start.UseShellExecute = false;
        start.CreateNoWindow = true;
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        start.EnvironmentVariables[SystemWrapperChildProbe.ModeVariable] = mode;
        start.EnvironmentVariables["DOTNET_DbgEnableMiniDump"] = "0";
        start.EnvironmentVariables["COMPlus_DbgEnableMiniDump"] = "0";
        return start;
    }

    internal static async Task<ChildResult> RunChildAsync(string mode, string? moduleOverride = null)
    {
        using Process process = new() { StartInfo = CreateChildStartInfo(mode, moduleOverride) };
        Assert.IsTrue(process.Start());
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync().TimeoutAfterAsync(ChildLimit);
            return new(process.ExitCode, await output, await error);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill();
                await process.WaitForExitAsync().TimeoutAfterAsync(ChildLimit);
            }
        }
    }

    internal static async Task<ChildResult> RunChildInCopyAsync(string mode)
    {
        string module = typeof(SystemWrappersTests).Assembly.Location;
        string root = Path.Combine(Path.GetTempPath(), "core-copy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            foreach (string file in Directory.GetFiles(Path.GetDirectoryName(module)!))
            {
                if (Path.GetExtension(file).ToLowerInvariant() is ".dll" or ".exe" or ".json" or ".config" or ".pdb")
                {
                    File.Copy(file, Path.Combine(root, Path.GetFileName(file)));
                }
            }

            return await RunChildAsync(mode, Path.Combine(root, Path.GetFileName(module)));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    internal static async Task ObserveOwnedDebuggerWaitAsync(string mode)
    {
        using Process process = new() { StartInfo = CreateChildStartInfo(mode) };
        Assert.IsTrue(process.Start());
        Task<string?> diagnostic = process.StandardOutput.ReadLineAsync();
        try
        {
            await diagnostic.TimeoutAfterAsync(ChildLimit);
            Assert.AreEqual(
                "Waiting for debugger to attach... Process Id: " + process.Id.ToString(CultureInfo.InvariantCulture) + ", Name: " + process.ProcessName,
                await diagnostic);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill();
            }

            await process.WaitForExitAsync(CancellationToken.None).TimeoutAfterAsync(ChildLimit);
        }
    }

    [TestMethod]
    public async Task FileSystemCaseProbe_FailedOwnedTempProbeFallsBackWithoutDeletingSentinel()
    {
        ChildResult child = await RunChildAsync("filesystem-case-fallback");

        Assert.AreEqual(0, child.ExitCode, child.Error);
        string[] records = child.Output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimStart('\uFEFF'))
            .Where(line => line.StartsWith("PROBE|", StringComparison.Ordinal))
            .Select(line => Encoding.UTF8.GetString(Convert.FromBase64String(line["PROBE|".Length..])))
            .ToArray();
        Assert.AreSequenceEqual(new[] { "case-sensitive:False", "sentinel:owned sentinel" }, records);
    }

    internal sealed record ChildResult(int ExitCode, string Output, string Error);
}

internal static class SystemWrapperChildProbe
{
    internal const string ModeVariable = "TESTFX_CORE_WRAPPER_CHILD_PROBE";

    internal static bool TryRun()
    {
        if (OperatingSystem.IsBrowser() || OperatingSystem.IsWasi())
        {
            return false;
        }

        string? mode = Environment.GetEnvironmentVariable(ModeVariable);
        if (mode is null)
        {
            return false;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            WindowsCrashReporting.DisableInteractiveReporting();
        }

        SystemEnvironment environment = new();
        switch (mode)
        {
            case "filesystem-case-fallback":
                {
                    string root = Path.Combine(Path.GetTempPath(), "case-fallback-" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(root);
                    try
                    {
                        string barrier = Path.Combine(root, "ordinary-file");
                        File.WriteAllText(barrier, "owned sentinel");
                        Environment.SetEnvironmentVariable("TEMP", barrier);
                        Environment.SetEnvironmentVariable("TMP", barrier);
                        Environment.SetEnvironmentVariable("TMPDIR", barrier);
                        Record("case-sensitive:" + Microsoft.Testing.Platform.OutputDevice.Terminal.FileUtilities.GetIsFileSystemCaseSensitive());
                        Record("sentinel:" + File.ReadAllText(barrier));
                    }
                    finally
                    {
                        Directory.Delete(root, recursive: true);
                    }

                    break;
                }

            case "exit":
                environment.Exit(37);
                break;
            case "fail-fast":
                Console.WriteLine("FAIL_FAST_WITHOUT_EXCEPTION");
                environment.FailFast("core-wrapper-fail-fast");
                break;
            case "fail-fast-exception":
                Console.WriteLine("FAIL_FAST_WITH_EXCEPTION");
                environment.FailFast("core-wrapper-fail-fast", new ProbeException());
                break;
            case "block":
                Console.WriteLine("READY");
                BlockUntilParentTerminates();
                break;
            case "long-running-failure":
                _ = new SystemTask().RunLongRunning(() => throw new ProbeException(), "fatal-probe", CancellationToken.None);
                BlockUntilParentTerminates();
                break;
            case "server-handler-task":
            case "server-handler-thread":
                using (ServerTestHost server = ServerTestHostTests.CreateUnhandledEventChild(Record))
                {
                    if (mode == "server-handler-task")
                    {
                        EventHandler<UnobservedTaskExceptionEventArgs> observed = (_, args) => Record("observed:" + args.Observed);
                        TaskScheduler.UnobservedTaskException += observed;
                        ForgetFaultedTask();
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        TaskScheduler.UnobservedTaskException -= observed;
                    }
                    else
                    {
                        new Thread(() => throw new ProbeException()).Start();
                        BlockUntilParentTerminates();
                    }
                }

                break;
            default:
                if (mode == "connection-wire")
                {
                    if (OperatingSystem.IsBrowser() || OperatingSystem.IsWasi())
                    {
                        throw new PlatformNotSupportedException("The owned wire probe requires desktop named pipes.");
                    }

                    DotnetTestConnectionTests.RunConnectionWireChildAsync(Record).GetAwaiter().GetResult();
                    break;
                }

                if (mode.StartsWith("pipe-eof-", StringComparison.Ordinal))
                {
                    IPCTests.RunPeerEofChildAsync(mode, Record).GetAwaiter().GetResult();
                    break;
                }

                if (mode.StartsWith("pipe-lifecycle-", StringComparison.Ordinal))
                {
                    IPCTests.RunLifecycleChildAsync(mode, Record).GetAwaiter().GetResult();
                    break;
                }

                if (mode.StartsWith("builder-", StringComparison.Ordinal))
                {
                    TestApplicationCreationTests.RunBuilderChildAsync(mode, Record).GetAwaiter().GetResult();
                    break;
                }

                if (!mode.StartsWith("handler-", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Unknown owned child probe.");
                }

                UnhandledExceptionHandler handler = new(new RecordingEnvironment(), new RecordingConsole(), isTestHost: mode.EndsWith("-host", StringComparison.Ordinal));
                handler.SetLogger(new RecordingLogger());
                handler.Subscribe();
                if (mode.StartsWith("handler-task-", StringComparison.Ordinal))
                {
                    ForgetFaultedTask();
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    environment.Exit(99);
                }
                else
                {
                    new Thread(() => throw new ProbeException()).Start();
                    BlockUntilParentTerminates();
                }

                break;
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ForgetFaultedTask()
        => _ = Task.FromException(new ProbeException());

    private static void BlockUntilParentTerminates()
    {
        using ManualResetEvent blocked = new(false);
        blocked.WaitOne();
    }

    private static void Record(string value)
        => Console.WriteLine("PROBE|" + Convert.ToBase64String(Encoding.UTF8.GetBytes(value)));

    [SupportedOSPlatform("windows")]
    private static class WindowsCrashReporting
    {
        internal static void DisableInteractiveReporting()
        {
            _ = SetErrorMode(0x0001 | 0x0002);
            _ = WerSetFlags(0x0020);
        }

        [DllImport("kernel32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern uint SetErrorMode(uint mode);

        [DllImport("kernel32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern int WerSetFlags(uint flags);
    }

    internal sealed class ProbeException() : Exception("probe-sentinel")
    {
        public override string ToString() => "PROBE_EXCEPTION";
    }

    private sealed class RecordingLogger : ILogger
    {
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Record("log-" + logLevel + ":" + formatter(state, exception));

        public Task LogAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Log(logLevel, state, exception, formatter);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingConsole : IConsole
    {
        public event ConsoleCancelEventHandler? CancelKeyPress
        {
            add { }
            remove { }
        }

        public int BufferHeight => 100;

        public int BufferWidth => 100;

        public int WindowHeight => 100;

        public int WindowWidth => 100;

        public bool IsOutputRedirected => true;

        public void WriteLine(string? value) => Record("console:" + value);

        public void WriteLine() => Record("console:");

        public void Write(string? value) => Record("console:" + value);

        public void Write(char value) => Record("console:" + value);

        public void Write(StringBuilder value) => Record("console:" + value);

        public void Clear() => throw new NotSupportedException();

        public void SetForegroundColor(ConsoleColor color) => throw new NotSupportedException();

        public ConsoleColor GetForegroundColor() => ConsoleColor.Gray;
    }

    private sealed class RecordingEnvironment : IEnvironment
    {
        public string CommandLine => string.Empty;

        public string MachineName => string.Empty;

        public string NewLine => Environment.NewLine;

        public int ProcessId => 0;

        public string OsVersion => string.Empty;

#if NETCOREAPP
        public string? ProcessPath => null;
#endif

        public string[] GetCommandLineArgs() => [];

        public string? GetEnvironmentVariable(string name) => null;

        public IDictionary GetEnvironmentVariables() => new Hashtable();

        public string GetFolderPath(Environment.SpecialFolder folder, Environment.SpecialFolderOption option) => string.Empty;

        public void SetEnvironmentVariable(string variable, string? value) => throw new NotSupportedException();

        public void FailFast(string? message, Exception? exception)
        {
            Record("fail-fast-exception:" + exception?.GetType().FullName);
            new SystemEnvironment().Exit(44);
        }

        public void FailFast(string? message)
        {
            Record("fail-fast-message");
            new SystemEnvironment().Exit(45);
        }

        public void Exit(int exitCode) => new SystemEnvironment().Exit(exitCode);
    }
}
