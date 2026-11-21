// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP && !WIN_UI
using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Services;

public sealed class TestContextAssertionFailureDiagnosticsResourceSnapshotMutationTests : TestContainer
{
    private const string ProcSelfIoPath = "/proc/self/io";

    private static readonly Type ResourceSnapshotType = typeof(TestContextImplementation).GetNestedType("ResourceSnapshot", BindingFlags.NonPublic)!;

    public void ParseCounterReadsTheIntegerAfterTheFirstSeparator()
    {
        (string Line, long? Expected)[] cases =
        [
            ("rchar: 123", 123),
            ("wchar:  5 ", 5),
            (":42", 42),
            ("rchar:0", 0),
            ("rchar: -7", -7),
            ("rchar: 9223372036854775807", long.MaxValue),
        ];

        foreach ((string line, long? expected) in cases)
        {
            ParseCounter(line).Should().Be(expected, because: $"'{line}' holds the integer after its first separator");
        }
    }

    public void ParseCounterRejectsLinesWithoutAnIntegerAfterTheFirstSeparator()
    {
        string[] lines =
        [
            "123",
            string.Empty,
            "rchar:",
            "rchar:   ",
            "a:b:7",
            "rchar: 1,234",
            "rchar: 1.5",
            "rchar: 12 34",
            "rchar: 9223372036854775808",
        ];

        foreach (string line in lines)
        {
            ParseCounter(line).Should().BeNull(because: $"'{line}' has no integer after its first separator");
        }
    }

    public void CaptureDoesNotForceAFullBlockingGarbageCollection()
    {
        MethodInfo capture = ResourceSnapshotType.GetMethod("Capture", BindingFlags.Public | BindingFlags.Static)!;
        const int Attempts = 10;
        int attemptsWithAGenerationTwoCollection = 0;

        // A collection triggered by something else can only hide the forced one occasionally, never on every attempt.
        for (int attempt = 0; attempt < Attempts; attempt++)
        {
            int collectionsBefore = GC.CollectionCount(2);
            capture.Invoke(null, null);
            if (GC.CollectionCount(2) != collectionsBefore)
            {
                attemptsWithAGenerationTwoCollection++;
            }
        }

        attemptsWithAGenerationTwoCollection.Should().BeLessThan(Attempts, because: "capturing a resource snapshot only reads the current managed heap size and must not run a full collection");
    }

    public void ProcessIoIsUnavailableWhenTheProcessHandleCannotBeObtainedOnWindows()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        using var process = new Process();

        (long? ReadBytes, long? WriteBytes) counters = TryCaptureProcessIo(process);

        counters.ReadBytes.Should().BeNull(because: "a process object without an associated process has no handle to query");
        counters.WriteBytes.Should().BeNull(because: "a process object without an associated process has no handle to query");
    }

    public void ProcessIoCountersAreReadFromProcSelfIoOnLinux()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || !TryReadProcSelfIo(out _))
        {
            return;
        }

        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            // Makes sure the write counter is not zero, so that it cannot be confused with an unrelated counter of the file.
            File.WriteAllText(path, "written bytes");
            using var process = Process.GetCurrentProcess();

            TryReadProcSelfIo(out (long ReadBytes, long WriteBytes) before).Should().BeTrue();
            (long? ReadBytes, long? WriteBytes) counters = TryCaptureProcessIo(process);
            TryReadProcSelfIo(out (long ReadBytes, long WriteBytes) after).Should().BeTrue();

            before.WriteBytes.Should().BePositive();
            counters.ReadBytes.Should().BeInRange(before.ReadBytes, after.ReadBytes, because: "rchar is the number of bytes read by the process and only grows");
            counters.WriteBytes.Should().BeInRange(before.WriteBytes, after.WriteBytes, because: "wchar is the number of bytes written by the process and only grows");
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static long? ParseCounter(string line)
        => (long?)ResourceSnapshotType.GetMethod("ParseCounter", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [line]);

    private static (long? ReadBytes, long? WriteBytes) TryCaptureProcessIo(Process process)
        => ((long? ReadBytes, long? WriteBytes))ResourceSnapshotType.GetMethod("TryCaptureProcessIo", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [process])!;

    private static bool TryReadProcSelfIo(out (long ReadBytes, long WriteBytes) counters)
    {
        counters = default;
        try
        {
            long? readBytes = null;
            long? writeBytes = null;
            foreach (string line in File.ReadAllLines(ProcSelfIoPath))
            {
                if (line.StartsWith("rchar:", StringComparison.Ordinal))
                {
                    readBytes = long.Parse(line.Substring(line.IndexOf(':') + 1).Trim(), CultureInfo.InvariantCulture);
                }
                else if (line.StartsWith("wchar:", StringComparison.Ordinal))
                {
                    writeBytes = long.Parse(line.Substring(line.IndexOf(':') + 1).Trim(), CultureInfo.InvariantCulture);
                }
            }

            if (readBytes is null || writeBytes is null)
            {
                return false;
            }

            counters = (readBytes.Value, writeBytes.Value);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
#endif
