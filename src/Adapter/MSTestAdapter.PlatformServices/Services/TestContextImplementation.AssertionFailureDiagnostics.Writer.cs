// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP && !WIN_UI
using System.Runtime.Serialization.Json;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

internal sealed partial class TestContextImplementation
{
    private const int MaximumActiveTests = 128;
    private const int MaximumStackFrames = 64;
    private const int MaximumArtifactBytes = 8 * 1024 * 1024;
    private const int MaximumIdentityLength = 2 * 1024;
    private const int MaximumMessageLength = 32 * 1024;
    private const int MaximumPathLength = 4 * 1024;
    private const int MaximumValueLength = 16 * 1024;
    private const string ArtifactFileNameFormat = "mstest-assertion-failure-state-attempt-{0}-invocation-{1}-capture-{2}.json";
    private const string DataContractSerializationJustification =
        "Assertion failure payload types are private, fixed, and only used when runtime code generation is available.";

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
            CurrentCulture = Truncate(CultureInfo.CurrentCulture.Name, MaximumIdentityLength)!,
            CurrentUICulture = Truncate(CultureInfo.CurrentUICulture.Name, MaximumIdentityLength)!,
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
            string? outputVolumePath;
            long outputVolumeAvailableFreeBytes;
            long outputVolumeTotalBytes;

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
                outputVolumePath = NativeMethods.GetVolumePathName(outputDirectory, volumePath, volumePath.Capacity)
                    ? Truncate(volumePath.ToString(), MaximumIdentityLength)
                    : Truncate(outputDirectory, MaximumIdentityLength);
                outputVolumeAvailableFreeBytes = checked((long)availableFreeBytes);
                outputVolumeTotalBytes = checked((long)totalBytes);
            }
            else
            {
                var drive = new DriveInfo(outputDirectory);
                outputVolumePath = Truncate(drive.Name, MaximumIdentityLength);
                outputVolumeAvailableFreeBytes = drive.AvailableFreeSpace;
                outputVolumeTotalBytes = drive.TotalSize;
            }

            processArtifact.OutputVolumePath = outputVolumePath;
            processArtifact.OutputVolumeAvailableFreeBytes = outputVolumeAvailableFreeBytes;
            processArtifact.OutputVolumeTotalBytes = outputVolumeTotalBytes;
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
}
#endif
