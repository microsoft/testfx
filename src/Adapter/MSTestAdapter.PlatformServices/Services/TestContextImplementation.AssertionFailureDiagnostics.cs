// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP && !WIN_UI
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
#endif

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

internal sealed partial class TestContextImplementation
{
#if !WINDOWS_UWP && !WIN_UI
    private const int MaximumActiveTests = 128;
    private const int MaximumCapturesPerAttempt = 3;
    private const int MaximumStackFrames = 64;
    private const int MaximumArtifactBytes = 8 * 1024 * 1024;
    private const int MaximumIdentityLength = 2 * 1024;
    private const int MaximumMessageLength = 32 * 1024;
    private const int MaximumPathLength = 4 * 1024;
    private const int MaximumValueLength = 16 * 1024;
    private const string ArtifactFileNameFormat = "mstest-assertion-failure-state-attempt-{0}-invocation-{1}-capture-{2}.json";
    private const string DataContractSerializationJustification =
        "Assertion failure payload types are private, fixed, and only used when runtime code generation is available.";

    private static readonly ConcurrentDictionary<long, ActiveTest> ActiveTests = new();
    private static readonly AsyncLocal<ActiveTest?> CurrentActiveTest = new();

    private static long s_nextActiveTestId;

    private ConcurrentQueue<string>? _completedAssertionFailureDiagnosticsPaths;
    private int _unscopedAssertionFailureCaptureCount;
    private int _retainAssertionFailureDiagnosticsDirectory;
#endif

    internal IDisposable? StartAssertionFailureDiagnosticsScope()
    {
#if WINDOWS_UWP || WIN_UI
        GC.KeepAlive(this);
        return null;
#else
        if (!MSTestSettings.CurrentSettings.CaptureAssertionFailureDiagnostics || !RuntimeContext.IsMultiThreaded)
        {
            return null;
        }

#if NET
        if (!RuntimeFeature.IsDynamicCodeSupported)
        {
            return null;
        }
#endif

        ActiveTest? previous = CurrentActiveTest.Value;
        try
        {
            ActiveTest activeTest = CreateActiveTest();
            ActiveTests[activeTest.Id] = activeTest;
            CurrentActiveTest.Value = activeTest;
            return new ActiveTestScope(activeTest, previous);
        }
        catch (Exception ex)
        {
            LogWarning(
                "Failed to initialize assertion failure diagnostics for test '{0}.{1}': {2}",
                FullyQualifiedTestClassName,
                TestName,
                ex);
            return null;
        }
#endif
    }

    internal static void CaptureAssertionFailureDiagnostics(string message, string? expected, string? actual)
    {
#if !WINDOWS_UWP && !WIN_UI
        if (!MSTestSettings.CurrentSettings.CaptureAssertionFailureDiagnostics || !RuntimeContext.IsMultiThreaded)
        {
            return;
        }

#if NET
        if (!RuntimeFeature.IsDynamicCodeSupported)
        {
            return;
        }
#endif

        ActiveTest? activeTest = CurrentActiveTest.Value;
        if (activeTest is null)
        {
            if (TestContext.Current is TestContextImplementation context)
            {
                context.CaptureUnscopedAssertionFailureDiagnostics(message, expected, actual);
            }

            return;
        }

        try
        {
            activeTest.Capture(message, expected, actual);
        }
        catch (Exception ex)
        {
            LogWarning(
                "Failed to capture assertion failure diagnostics for test '{0}': {1}",
                activeTest.FullyQualifiedName,
                ex);
        }
#endif
    }

    internal void FinalizeAssertionFailureDiagnostics(UnitTestOutcome outcome)
    {
#if WINDOWS_UWP || WIN_UI
        GC.KeepAlive(this);
#else
        ActiveTest? activeTest = CurrentActiveTest.Value;
        if (activeTest is null || !ReferenceEquals(activeTest.Context, this))
        {
            return;
        }

        foreach (string path in activeTest.CloseAndDrain())
        {
            if (outcome != UnitTestOutcome.Passed)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        AddResultFile(path);
                        RecordCompletedAssertionFailureDiagnosticsPath(path);
                        Volatile.Write(ref _retainAssertionFailureDiagnosticsDirectory, 1);
                    }
                }
                catch (Exception ex)
                {
                    LogWarning(
                        "Failed to attach assertion failure diagnostics artifact '{0}': {1}",
                        path,
                        ex);
                }

                continue;
            }

            try
            {
                File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                LogWarning(
                    "Failed to delete assertion failure diagnostics artifact '{0}' for a non-failing test: {1}",
                    path,
                    ex);
            }
        }
#endif
    }

#if !WINDOWS_UWP && !WIN_UI
    internal void FinalizeAssertionFailureDiagnosticsExecution(TestResult[] results)
    {
        ConcurrentQueue<string>? completedPaths = Interlocked.Exchange(ref _completedAssertionFailureDiagnosticsPaths, null);
        Interlocked.Exchange(ref _unscopedAssertionFailureCaptureCount, 0);
        if (completedPaths is null)
        {
            return;
        }

        StringComparer pathComparer = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        string[] paths = completedPaths.Distinct(pathComparer)
            .ToArray();
        TestResult? targetResult = results.FirstOrDefault(static result => result.Outcome != UnitTestOutcome.Passed);
        if (targetResult is null)
        {
            foreach (TestResult result in results)
            {
                if (result.ResultFiles is { Count: > 0 } resultFiles)
                {
                    var retainedFiles = resultFiles.Where(path => !paths.Contains(path, pathComparer)).ToList();
                    result.ResultFiles = retainedFiles.Count == 0 ? null : retainedFiles;
                }
            }

            foreach (string path in paths)
            {
                TryDeleteArtifact(path, "completed diagnostics for a passing test");
            }

            return;
        }

        var existingPaths = new HashSet<string>(
            results.SelectMany(static result => result.ResultFiles ?? []),
            pathComparer);
        foreach (string path in paths)
        {
            if (existingPaths.Add(path))
            {
                (targetResult.ResultFiles ??= []).Add(path);
            }
        }

        Volatile.Write(ref _retainAssertionFailureDiagnosticsDirectory, 1);
    }

    internal void TransferAssertionFailureDiagnosticsTo(TestContextImplementation destination)
    {
        ConcurrentQueue<string>? completedPaths = Interlocked.Exchange(ref _completedAssertionFailureDiagnosticsPaths, null);
        Interlocked.Exchange(ref _unscopedAssertionFailureCaptureCount, 0);
        if (completedPaths is null)
        {
            return;
        }

        foreach (string path in completedPaths)
        {
            destination.RecordCompletedAssertionFailureDiagnosticsPath(path);
        }
    }

    private ActiveTest CreateActiveTest()
        => new(
            Interlocked.Increment(ref s_nextActiveTestId),
            this,
            FullyQualifiedTestClassName,
            TestName,
            TestDisplayName,
            Context.TestRunCount,
            DateTimeOffset.UtcNow,
            Stopwatch.GetTimestamp(),
            ResourceSnapshot.CaptureBaseline());

    private void CaptureUnscopedAssertionFailureDiagnostics(string message, string? expected, string? actual)
    {
        int captureIndex = Interlocked.Increment(ref _unscopedAssertionFailureCaptureCount);
        if (captureIndex > MaximumCapturesPerAttempt)
        {
            return;
        }

        ActiveTest? activeTest = null;
        try
        {
            activeTest = CreateActiveTest();
            ActiveTests[activeTest.Id] = activeTest;
            activeTest.Capture(message, expected, actual);
            foreach (string path in activeTest.CloseAndDrain())
            {
                RecordCompletedAssertionFailureDiagnosticsPath(path);
            }
        }
        catch (Exception ex)
        {
            LogWarning(
                "Failed to capture assertion failure diagnostics outside a test method invocation for test '{0}.{1}': {2}",
                FullyQualifiedTestClassName,
                TestName,
                ex);
        }
        finally
        {
            if (activeTest is not null)
            {
                ActiveTests.TryRemove(activeTest.Id, out _);
                foreach (string path in activeTest.CloseAndDrain())
                {
                    TryDeleteArtifact(path, "unfinalized diagnostics outside a test method invocation");
                }
            }
        }
    }

    private void RecordCompletedAssertionFailureDiagnosticsPath(string path)
        => LazyInitializer.EnsureInitialized(ref _completedAssertionFailureDiagnosticsPaths, static () => new())!.Enqueue(path);

    private static void TryDeleteArtifact(string path, string reason)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogWarning("Failed to delete assertion failure diagnostics artifact '{0}' ({1}): {2}", path, reason, ex);
        }
    }

    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:Members attributed with RequiresUnreferencedCode may break when trimming", Justification = DataContractSerializationJustification)]
    [UnconditionalSuppressMessage("Aot", "IL3050:Avoid calling members annotated with 'RequiresDynamicCodeAttribute' when publishing as Native AOT", Justification = DataContractSerializationJustification)]
#if NET
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(AssertionFailureArtifact))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(AssertionArtifact))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(TestArtifact))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(ThreadArtifact))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(ActiveTestArtifact))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(ProcessArtifact))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(StackFrameArtifact))]
#endif
    private static string WriteAssertionFailureDiagnostics(ActiveTest activeTest, int captureIndex, string message, string? expected, string? actual)
    {
        string outputDirectory = activeTest.Context.TestTempDirectory
            ?? throw new InvalidOperationException("A per-test temporary directory is required to capture assertion failure diagnostics.");
        string outputPath = Path.Combine(
            outputDirectory,
            string.Format(CultureInfo.InvariantCulture, ArtifactFileNameFormat, activeTest.Attempt, activeTest.Id, captureIndex));
        DateTimeOffset capturedAtUtc = DateTimeOffset.UtcNow;
        ActiveTest[] activeTestsSnapshot = ActiveTests.Values.ToArray();
        long capturedAtTimestamp = Stopwatch.GetTimestamp();
        TimeSpan elapsed = GetElapsed(activeTest.StartTimestamp, capturedAtTimestamp);
        var currentResources = ResourceSnapshot.Capture();

        ActiveTestArtifact[] runningTests = activeTestsSnapshot
            .OrderBy(test => test.Id == activeTest.Id ? 0 : 1)
            .ThenBy(static test => test.FullyQualifiedName, StringComparer.Ordinal)
            .ThenBy(static test => test.DisplayName, StringComparer.Ordinal)
            .ThenBy(static test => test.Id)
            .Take(MaximumActiveTests)
            .Select(test => new ActiveTestArtifact
            {
                FullyQualifiedName = Truncate(test.FullyQualifiedName, MaximumIdentityLength)!,
                DisplayName = Truncate(test.DisplayName, MaximumIdentityLength)!,
                Attempt = test.Attempt,
                StartedAtUtc = test.StartedAtUtc.ToString("O", CultureInfo.InvariantCulture),
                ElapsedMilliseconds = Math.Max(0, GetElapsed(test.StartTimestamp, capturedAtTimestamp).TotalMilliseconds),
                IsFailingTest = test.Id == activeTest.Id,
            })
            .ToArray();

        var artifact = new AssertionFailureArtifact
        {
            SchemaVersion = 1,
            CaptureIndex = captureIndex,
            CapturedAtUtc = capturedAtUtc.ToString("O", CultureInfo.InvariantCulture),
            Assertion = new AssertionArtifact
            {
                Message = Truncate(message, MaximumMessageLength),
                Expected = Truncate(expected, MaximumValueLength),
                Actual = Truncate(actual, MaximumValueLength),
            },
            Test = new TestArtifact
            {
                FullyQualifiedName = Truncate(activeTest.FullyQualifiedName, MaximumIdentityLength)!,
                DisplayName = Truncate(activeTest.DisplayName, MaximumIdentityLength)!,
                Attempt = activeTest.Attempt,
                ElapsedMilliseconds = elapsed.TotalMilliseconds,
            },
            Thread = new ThreadArtifact
            {
                ManagedThreadId = Environment.CurrentManagedThreadId,
                Name = Truncate(Thread.CurrentThread.Name, MaximumIdentityLength),
            },
            ActiveTests = runningTests,
            ActiveTestsTruncated = activeTestsSnapshot.Length > runningTests.Length,
            Process = CreateProcessArtifact(activeTest.StartResources, currentResources, elapsed, outputDirectory),
            StackFrames = CaptureStackFrames(),
        };

        try
        {
            var serializer = new DataContractJsonSerializer(typeof(AssertionFailureArtifact));
            using FileStream stream = new(outputPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            serializer.WriteObject(stream, artifact);
            if (stream.Length > MaximumArtifactBytes)
            {
                throw new InvalidOperationException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Assertion failure diagnostics artifact exceeded the {0}-byte limit.",
                        MaximumArtifactBytes));
            }
        }
        catch
        {
            try
            {
                File.Delete(outputPath);
            }
            catch (Exception cleanupException) when (cleanupException is IOException or UnauthorizedAccessException)
            {
                LogWarning(
                    "Failed to delete incomplete assertion failure diagnostics artifact '{0}': {1}",
                    outputPath,
                    cleanupException);
            }

            throw;
        }

        return outputPath;
    }

    private static ProcessArtifact CreateProcessArtifact(ResourceBaseline start, ResourceSnapshot current, TimeSpan elapsed, string outputDirectory)
    {
        double? normalizedCpuPercent = elapsed > TimeSpan.Zero && current.TotalProcessorTime >= start.TotalProcessorTime
            ? Math.Min(100, (current.TotalProcessorTime - start.TotalProcessorTime).TotalMilliseconds / elapsed.TotalMilliseconds / Math.Max(1, Environment.ProcessorCount) * 100)
            : null;

        long? readBytes = SubtractCounters(current.IoReadBytes, start.IoReadBytes);
        long? writeBytes = SubtractCounters(current.IoWriteBytes, start.IoWriteBytes);
        double elapsedSeconds = elapsed.TotalSeconds;

        var processArtifact = new ProcessArtifact
        {
            ProcessId = current.ProcessId,
            ProcessName = Truncate(current.ProcessName, MaximumIdentityLength),
#if NETCOREAPP
            FrameworkDescription = Truncate(RuntimeInformation.FrameworkDescription, MaximumIdentityLength)!,
            OperatingSystemDescription = Truncate(RuntimeInformation.OSDescription, MaximumIdentityLength)!,
            ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
#else
            FrameworkDescription = Truncate(RuntimeEnvironment.GetSystemVersion(), MaximumIdentityLength)!,
            OperatingSystemDescription = Truncate(Environment.OSVersion.VersionString, MaximumIdentityLength)!,
            ProcessArchitecture = IntPtr.Size == 8 ? "X64" : "X86",
#endif
            ProcessorCount = Environment.ProcessorCount,
            CpuPercentDuringTest = normalizedCpuPercent,
            TotalProcessorTimeMilliseconds = current.TotalProcessorTime.TotalMilliseconds,
            WorkingSetBytes = current.WorkingSetBytes,
            PrivateMemoryBytes = current.PrivateMemoryBytes,
            ManagedHeapBytes = current.ManagedHeapBytes,
            GcMemoryLoadBytes = current.GcMemoryLoadBytes,
            GcTotalAvailableMemoryBytes = current.GcTotalAvailableMemoryBytes,
            ProcessIoAvailable = current.IoReadBytes is not null
                && current.IoWriteBytes is not null
                && start.IoReadBytes is not null
                && start.IoWriteBytes is not null,
            ProcessIoReadBytesDuringTest = readBytes,
            ProcessIoWriteBytesDuringTest = writeBytes,
            ProcessIoReadBytesPerSecond = readBytes is not null && elapsedSeconds > 0 ? readBytes / elapsedSeconds : null,
            ProcessIoWriteBytesPerSecond = writeBytes is not null && elapsedSeconds > 0 ? writeBytes / elapsedSeconds : null,
        };

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (!NativeMethods.GetDiskFreeSpaceEx(
                    outputDirectory,
                    out ulong availableFreeBytes,
                    out ulong totalBytes,
                    out _))
                {
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                }

                var volumePath = new StringBuilder(capacity: 32_768);
                processArtifact.OutputVolumePath = NativeMethods.GetVolumePathName(outputDirectory, volumePath, volumePath.Capacity)
                    ? Truncate(volumePath.ToString(), MaximumIdentityLength)
                    : Truncate(outputDirectory, MaximumIdentityLength);
                processArtifact.OutputVolumeAvailableFreeBytes = checked((long)availableFreeBytes);
                processArtifact.OutputVolumeTotalBytes = checked((long)totalBytes);
            }
            else
            {
                var drive = new DriveInfo(outputDirectory);
                processArtifact.OutputVolumePath = Truncate(drive.Name, MaximumIdentityLength);
                processArtifact.OutputVolumeAvailableFreeBytes = drive.AvailableFreeSpace;
                processArtifact.OutputVolumeTotalBytes = drive.TotalSize;
            }
        }
        catch (Exception ex) when (ex is ArgumentException
            or IOException
            or InvalidOperationException
            or OverflowException
            or System.ComponentModel.Win32Exception
            or UnauthorizedAccessException)
        {
            processArtifact.OutputVolumeError = Truncate(ex.Message, MaximumIdentityLength);
        }

        return processArtifact;
    }

    private static void LogWarning(string format, params object?[] args)
    {
        try
        {
            PlatformServiceProvider.Instance.AdapterTraceLogger.Warning(format, args);
        }
        catch (Exception)
        {
            // Diagnostics must never replace or mask the original assertion failure.
        }
    }

    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:Members attributed with RequiresUnreferencedCode may break when trimming", Justification = "Stack inspection records best-effort method metadata only; missing trimmed metadata is represented by null fields.")]
    private static StackFrameArtifact[] CaptureStackFrames()
    {
        StackFrame[]? frames = new StackTrace(skipFrames: 1, fNeedFileInfo: true).GetFrames();
        return frames is null
            ? []
            : frames
                .Take(MaximumStackFrames)
                .Select(static frame =>
                {
                    MethodBase? method = frame.GetMethod();
                    return new StackFrameArtifact
                    {
                        Method = method is null
                            ? null
                            : Truncate($"{method.DeclaringType?.FullName ?? "<global>"}.{method.Name}", MaximumPathLength),
                        Assembly = Truncate(method?.DeclaringType?.Assembly.GetName().Name, MaximumIdentityLength),
                        File = Truncate(frame.GetFileName(), MaximumPathLength),
                        Line = frame.GetFileLineNumber(),
                        Column = frame.GetFileColumnNumber(),
                        IsFrameworkFrame = method?.DeclaringType?.Assembly == typeof(Assert).Assembly
                            || method?.DeclaringType?.Assembly == typeof(TestContextImplementation).Assembly,
                    };
                })
                .ToArray();
    }

    private static TimeSpan GetElapsed(long startTimestamp, long endTimestamp)
        => TimeSpan.FromSeconds((endTimestamp - startTimestamp) / (double)Stopwatch.Frequency);

    private static long? SubtractCounters(long? current, long? start)
        => current is not null && start is not null && current >= start
            ? current.Value - start.Value
            : null;

    private static string? Truncate(string? value, int maximumLength)
    {
        if (value is null)
        {
            return null;
        }

        const string TruncationSuffix = "... <truncated>";
        bool requiresTruncation = value.Length > maximumLength;
        int contentLimit = requiresTruncation
            ? Math.Max(0, maximumLength - TruncationSuffix.Length)
            : maximumLength;
        var builder = new StringBuilder(Math.Min(maximumLength, value.Length + TruncationSuffix.Length));
        int index = 0;
        while (index < value.Length && builder.Length < contentLimit)
        {
            char current = value[index];
            if (char.IsHighSurrogate(current))
            {
                if (index + 1 < value.Length && char.IsLowSurrogate(value[index + 1]))
                {
                    if (builder.Length + 2 > contentLimit)
                    {
                        break;
                    }

                    builder.Append(current);
                    builder.Append(value[index + 1]);
                    index += 2;
                    continue;
                }

                builder.Append('\uFFFD');
                index++;
                continue;
            }

            builder.Append(char.IsLowSurrogate(current) ? '\uFFFD' : current);
            index++;
        }

        if (index < value.Length)
        {
            builder.Append(TruncationSuffix);
        }

        return builder.ToString();
    }

    private sealed class ActiveTestScope : IDisposable
    {
        private readonly ActiveTest _activeTest;
        private readonly ActiveTest? _previous;
        private int _disposed;

        public ActiveTestScope(ActiveTest activeTest, ActiveTest? previous)
        {
            _activeTest = activeTest;
            _previous = previous;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            ActiveTests.TryRemove(_activeTest.Id, out _);
            foreach (string path in _activeTest.CloseAndDrain())
            {
                try
                {
                    File.Delete(path);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    LogWarning("Failed to delete unfinalized assertion failure diagnostics artifact '{0}': {1}", path, ex);
                }
            }

            if (ReferenceEquals(CurrentActiveTest.Value, _activeTest))
            {
                CurrentActiveTest.Value = _previous;
            }
        }
    }

    private sealed class ActiveTest
    {
#if NET9_0_OR_GREATER
        private readonly Lock _captureLock = new();
#else
        private readonly object _captureLock = new();
#endif
        private readonly Queue<string> _pendingPaths = new();
        private int _captureCount;
        private bool _closed;

        public ActiveTest(
            long id,
            TestContextImplementation context,
            string testClassName,
            string testName,
            string? displayName,
            int attempt,
            DateTimeOffset startedAtUtc,
            long startTimestamp,
            ResourceBaseline startResources)
        {
            Id = id;
            Context = context;
            FullyQualifiedName = $"{testClassName}.{testName}";
            DisplayName = displayName ?? testName;
            Attempt = attempt;
            StartedAtUtc = startedAtUtc;
            StartTimestamp = startTimestamp;
            StartResources = startResources;
        }

        public long Id { get; }

        public TestContextImplementation Context { get; }

        public string FullyQualifiedName { get; }

        public string DisplayName { get; }

        public int Attempt { get; }

        public DateTimeOffset StartedAtUtc { get; }

        public long StartTimestamp { get; }

        public ResourceBaseline StartResources { get; }

        public void Capture(string message, string? expected, string? actual)
        {
            lock (_captureLock)
            {
                if (_closed || _captureCount >= MaximumCapturesPerAttempt)
                {
                    return;
                }

                int captureIndex = ++_captureCount;
                _pendingPaths.Enqueue(WriteAssertionFailureDiagnostics(this, captureIndex, message, expected, actual));
            }
        }

        public string[] CloseAndDrain()
        {
            lock (_captureLock)
            {
                if (_closed)
                {
                    return [];
                }

                _closed = true;
                string[] paths = _pendingPaths.ToArray();
                _pendingPaths.Clear();
                return paths;
            }
        }
    }

    private readonly record struct ResourceBaseline(
        TimeSpan TotalProcessorTime,
        long? IoReadBytes,
        long? IoWriteBytes);

    private readonly struct ResourceSnapshot
    {
        private ResourceSnapshot(
            int processId,
            string? processName,
            TimeSpan totalProcessorTime,
            long workingSetBytes,
            long privateMemoryBytes,
            long managedHeapBytes,
            long? memoryLoadBytes,
            long? totalAvailableMemoryBytes,
            long? ioReadBytes,
            long? ioWriteBytes)
        {
            ProcessId = processId;
            ProcessName = processName;
            TotalProcessorTime = totalProcessorTime;
            WorkingSetBytes = workingSetBytes;
            PrivateMemoryBytes = privateMemoryBytes;
            ManagedHeapBytes = managedHeapBytes;
            GcMemoryLoadBytes = memoryLoadBytes;
            GcTotalAvailableMemoryBytes = totalAvailableMemoryBytes;
            IoReadBytes = ioReadBytes;
            IoWriteBytes = ioWriteBytes;
        }

        public int ProcessId { get; }

        public string? ProcessName { get; }

        public TimeSpan TotalProcessorTime { get; }

        public long WorkingSetBytes { get; }

        public long PrivateMemoryBytes { get; }

        public long ManagedHeapBytes { get; }

        public long? GcMemoryLoadBytes { get; }

        public long? GcTotalAvailableMemoryBytes { get; }

        public long? IoReadBytes { get; }

        public long? IoWriteBytes { get; }

        public static ResourceBaseline CaptureBaseline()
        {
            using var process = Process.GetCurrentProcess();
            (long? ioReadBytes, long? ioWriteBytes) = TryCaptureProcessIo(process);
            return new ResourceBaseline(process.TotalProcessorTime, ioReadBytes, ioWriteBytes);
        }

        public static ResourceSnapshot Capture()
        {
            using var process = Process.GetCurrentProcess();
            long? memoryLoadBytes = null;
            long? totalAvailableMemoryBytes = null;
#if NETCOREAPP
            GCMemoryInfo gcMemoryInfo = GC.GetGCMemoryInfo();
            memoryLoadBytes = gcMemoryInfo.MemoryLoadBytes;
            totalAvailableMemoryBytes = gcMemoryInfo.TotalAvailableMemoryBytes;
#endif
            (long? ioReadBytes, long? ioWriteBytes) = TryCaptureProcessIo(process);

            return new ResourceSnapshot(
                process.Id,
                process.ProcessName,
                process.TotalProcessorTime,
                process.WorkingSet64,
                process.PrivateMemorySize64,
                GC.GetTotalMemory(forceFullCollection: false),
                memoryLoadBytes,
                totalAvailableMemoryBytes,
                ioReadBytes,
                ioWriteBytes);
        }

        private static (long? ReadBytes, long? WriteBytes) TryCaptureProcessIo(Process process)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    return NativeMethods.GetProcessIoCounters(process.Handle, out IoCounters counters)
                        ? (checked((long)counters.ReadTransferCount), checked((long)counters.WriteTransferCount))
                        : default;
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or OverflowException)
                {
                    return default;
                }
            }

            if (File.Exists("/proc/self/io"))
            {
                try
                {
                    long? readBytes = null;
                    long? writeBytes = null;
                    foreach (string line in File.ReadLines("/proc/self/io"))
                    {
                        if (line.StartsWith("rchar:", StringComparison.Ordinal))
                        {
                            readBytes = ParseCounter(line);
                        }
                        else if (line.StartsWith("wchar:", StringComparison.Ordinal))
                        {
                            writeBytes = ParseCounter(line);
                        }
                    }

                    return (readBytes, writeBytes);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    return default;
                }
            }

            return default;
        }

        private static long? ParseCounter(string line)
        {
            int separatorIndex = line.IndexOf(':');
            return separatorIndex >= 0
                && long.TryParse(line.Substring(separatorIndex + 1).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long value)
                    ? value
                    : null;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    private static class NativeMethods
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetProcessIoCounters(IntPtr processHandle, out IoCounters ioCounters);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetDiskFreeSpaceEx(
            string directoryName,
            out ulong freeBytesAvailable,
            out ulong totalNumberOfBytes,
            out ulong totalNumberOfFreeBytes);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetVolumePathName(string fileName, StringBuilder volumePathName, int bufferLength);
    }

    [DataContract]
    private sealed class AssertionFailureArtifact
    {
        [DataMember(Name = "schemaVersion", Order = 0)]
        public int SchemaVersion { get; set; }

        [DataMember(Name = "captureIndex", Order = 1)]
        public int CaptureIndex { get; set; }

        [DataMember(Name = "capturedAtUtc", Order = 2)]
        public string CapturedAtUtc { get; set; } = null!;

        [DataMember(Name = "assertion", Order = 3)]
        public AssertionArtifact Assertion { get; set; } = null!;

        [DataMember(Name = "test", Order = 4)]
        public TestArtifact Test { get; set; } = null!;

        [DataMember(Name = "thread", Order = 5)]
        public ThreadArtifact Thread { get; set; } = null!;

        [DataMember(Name = "activeTests", Order = 6)]
        public ActiveTestArtifact[] ActiveTests { get; set; } = null!;

        [DataMember(Name = "activeTestsTruncated", Order = 7)]
        public bool ActiveTestsTruncated { get; set; }

        [DataMember(Name = "process", Order = 8)]
        public ProcessArtifact Process { get; set; } = null!;

        [DataMember(Name = "stackFrames", Order = 9)]
        public StackFrameArtifact[] StackFrames { get; set; } = null!;
    }

    [DataContract]
    private sealed class AssertionArtifact
    {
        [DataMember(Name = "message", Order = 0)]
        public string? Message { get; set; }

        [DataMember(Name = "expected", Order = 1, EmitDefaultValue = false)]
        public string? Expected { get; set; }

        [DataMember(Name = "actual", Order = 2, EmitDefaultValue = false)]
        public string? Actual { get; set; }
    }

    [DataContract]
    private sealed class TestArtifact
    {
        [DataMember(Name = "fullyQualifiedName", Order = 0)]
        public string FullyQualifiedName { get; set; } = null!;

        [DataMember(Name = "displayName", Order = 1)]
        public string DisplayName { get; set; } = null!;

        [DataMember(Name = "attempt", Order = 2)]
        public int Attempt { get; set; }

        [DataMember(Name = "elapsedMilliseconds", Order = 3)]
        public double ElapsedMilliseconds { get; set; }
    }

    [DataContract]
    private sealed class ThreadArtifact
    {
        [DataMember(Name = "managedThreadId", Order = 0)]
        public int ManagedThreadId { get; set; }

        [DataMember(Name = "name", Order = 1, EmitDefaultValue = false)]
        public string? Name { get; set; }
    }

    [DataContract]
    private sealed class ActiveTestArtifact
    {
        [DataMember(Name = "fullyQualifiedName", Order = 0)]
        public string FullyQualifiedName { get; set; } = null!;

        [DataMember(Name = "displayName", Order = 1)]
        public string DisplayName { get; set; } = null!;

        [DataMember(Name = "attempt", Order = 2)]
        public int Attempt { get; set; }

        [DataMember(Name = "startedAtUtc", Order = 3)]
        public string StartedAtUtc { get; set; } = null!;

        [DataMember(Name = "elapsedMilliseconds", Order = 4)]
        public double ElapsedMilliseconds { get; set; }

        [DataMember(Name = "isFailingTest", Order = 5)]
        public bool IsFailingTest { get; set; }
    }

    [DataContract]
    private sealed class ProcessArtifact
    {
        [DataMember(Name = "processId", Order = 0)]
        public int ProcessId { get; set; }

        [DataMember(Name = "processName", Order = 1, EmitDefaultValue = false)]
        public string? ProcessName { get; set; }

        [DataMember(Name = "frameworkDescription", Order = 2)]
        public string FrameworkDescription { get; set; } = null!;

        [DataMember(Name = "operatingSystemDescription", Order = 3)]
        public string OperatingSystemDescription { get; set; } = null!;

        [DataMember(Name = "processArchitecture", Order = 4)]
        public string ProcessArchitecture { get; set; } = null!;

        [DataMember(Name = "processorCount", Order = 5)]
        public int ProcessorCount { get; set; }

        [DataMember(Name = "cpuPercentDuringTest", Order = 6, EmitDefaultValue = false)]
        public double? CpuPercentDuringTest { get; set; }

        [DataMember(Name = "totalProcessorTimeMilliseconds", Order = 7)]
        public double TotalProcessorTimeMilliseconds { get; set; }

        [DataMember(Name = "workingSetBytes", Order = 8)]
        public long WorkingSetBytes { get; set; }

        [DataMember(Name = "privateMemoryBytes", Order = 9)]
        public long PrivateMemoryBytes { get; set; }

        [DataMember(Name = "managedHeapBytes", Order = 10)]
        public long ManagedHeapBytes { get; set; }

        [DataMember(Name = "gcMemoryLoadBytes", Order = 11, EmitDefaultValue = false)]
        public long? GcMemoryLoadBytes { get; set; }

        [DataMember(Name = "gcTotalAvailableMemoryBytes", Order = 12, EmitDefaultValue = false)]
        public long? GcTotalAvailableMemoryBytes { get; set; }

        [DataMember(Name = "processIoAvailable", Order = 13)]
        public bool ProcessIoAvailable { get; set; }

        [DataMember(Name = "processIoReadBytesDuringTest", Order = 14, EmitDefaultValue = false)]
        public long? ProcessIoReadBytesDuringTest { get; set; }

        [DataMember(Name = "processIoWriteBytesDuringTest", Order = 15, EmitDefaultValue = false)]
        public long? ProcessIoWriteBytesDuringTest { get; set; }

        [DataMember(Name = "processIoReadBytesPerSecond", Order = 16, EmitDefaultValue = false)]
        public double? ProcessIoReadBytesPerSecond { get; set; }

        [DataMember(Name = "processIoWriteBytesPerSecond", Order = 17, EmitDefaultValue = false)]
        public double? ProcessIoWriteBytesPerSecond { get; set; }

        [DataMember(Name = "outputVolumePath", Order = 18, EmitDefaultValue = false)]
        public string? OutputVolumePath { get; set; }

        [DataMember(Name = "outputVolumeAvailableFreeBytes", Order = 19, EmitDefaultValue = false)]
        public long? OutputVolumeAvailableFreeBytes { get; set; }

        [DataMember(Name = "outputVolumeTotalBytes", Order = 20, EmitDefaultValue = false)]
        public long? OutputVolumeTotalBytes { get; set; }

        [DataMember(Name = "outputVolumeError", Order = 21, EmitDefaultValue = false)]
        public string? OutputVolumeError { get; set; }
    }

    [DataContract]
    private sealed class StackFrameArtifact
    {
        [DataMember(Name = "method", Order = 0, EmitDefaultValue = false)]
        public string? Method { get; set; }

        [DataMember(Name = "assembly", Order = 1, EmitDefaultValue = false)]
        public string? Assembly { get; set; }

        [DataMember(Name = "file", Order = 2, EmitDefaultValue = false)]
        public string? File { get; set; }

        [DataMember(Name = "line", Order = 3)]
        public int Line { get; set; }

        [DataMember(Name = "column", Order = 4)]
        public int Column { get; set; }

        [DataMember(Name = "isFrameworkFrame", Order = 5)]
        public bool IsFrameworkFrame { get; set; }
    }
#endif
}
