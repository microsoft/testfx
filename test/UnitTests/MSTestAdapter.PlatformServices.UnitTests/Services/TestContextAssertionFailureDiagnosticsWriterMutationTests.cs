// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP && !WIN_UI
#if NET
using System.Reflection.Emit;
#endif
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using Moq;

using TestFramework.ForTestingMSTest;

using ITestMethod = Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ObjectModel.ITestMethod;

namespace MSTestAdapter.PlatformServices.UnitTests.Services;

public sealed class TestContextAssertionFailureDiagnosticsWriterMutationTests : TestContainer
{
    private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const string ThisFileName = nameof(TestContextAssertionFailureDiagnosticsWriterMutationTests) + ".cs";

    private static readonly Type ActiveTestType = GetNestedType("ActiveTest");
    private static readonly Type ResourceBaselineType = GetNestedType("ResourceBaseline");
    private static readonly Type ResourceSnapshotType = GetNestedType("ResourceSnapshot");
    private static readonly MethodInfo WriteMethod = GetStaticMethod("WriteAssertionFailureDiagnostics");
    private static readonly MethodInfo CreateProcessArtifactMethod = GetStaticMethod("CreateProcessArtifact");
    private static readonly MethodInfo CaptureStackFramesMethod = GetStaticMethod("CaptureStackFrames");
    private static readonly MethodInfo GetElapsedMethod = GetStaticMethod("GetElapsed");
    private static readonly MethodInfo SubtractCountersMethod = GetStaticMethod("SubtractCounters");
    private static readonly IDictionary ActiveTests = (IDictionary)typeof(TestContextImplementation).GetField("ActiveTests", StaticMembers)!.GetValue(null)!;

    public void ActiveTestsAreMeasuredUntilTheCaptureAndNeverReportANegativeElapsedTime()
    {
        using DiagnosticsDirectory directory = new();
        using TestContextImplementation context = CreateContext(directory.Path);
        long now = Stopwatch.GetTimestamp();
        object failing = CreateActiveTest(context, -1_000_001, "Tests.Failing", now);
        object started = CreateActiveTest(context, -1_000_002, "Tests.Started", now - (5 * Stopwatch.Frequency));
        object notYetStarted = CreateActiveTest(context, -1_000_003, "Tests.NotYetStarted", now + (5 * Stopwatch.Frequency));

        using (RegisterActiveTests(failing, started, notYetStarted))
        using (JsonDocument artifact = WriteArtifact(failing, "message", null, null))
        {
            JsonElement[] active = [.. artifact.RootElement.GetProperty("activeTests").EnumerateArray()];

            active.Select(value => value.GetProperty("fullyQualifiedName").GetString())
                .Should().Equal("Tests.Failing.Runs", "Tests.NotYetStarted.Runs", "Tests.Started.Runs");
            active[0].GetProperty("elapsedMilliseconds").GetDouble().Should().BeInRange(0, 65_000);
            active[1].GetProperty("elapsedMilliseconds").GetDouble().Should().Be(0, "a test that has not started yet has no elapsed time");
            active[2].GetProperty("elapsedMilliseconds").GetDouble().Should().BeInRange(5_000, 65_000);
        }
    }

    public void CaptureTimestampIsWrittenAsAUtcRoundTripInstant()
    {
        using DiagnosticsDirectory directory = new();
        using TestContextImplementation context = CreateContext(directory.Path);
        object failing = CreateActiveTest(context, -1_000_001, "Tests.Failing", Stopwatch.GetTimestamp());

        using (RegisterActiveTests(failing))
        using (JsonDocument artifact = WriteArtifact(failing, "message", null, null))
        {
            string capturedAt = artifact.RootElement.GetProperty("capturedAtUtc").GetString()!;

            capturedAt.Should().MatchRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}\+00:00$");
            DateTimeOffset.ParseExact(capturedAt, "O", CultureInfo.InvariantCulture).Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        }
    }

    public void CpuUsageIsTheProcessorTimeConsumedDuringTheTestSharedAcrossAllCores()
    {
        int cores = Math.Max(1, Environment.ProcessorCount);

        // Half a second of processor time during one second of wall-clock time is half of one core.
        CpuPercent(TimeSpan.FromSeconds(1), startCpu: TimeSpan.FromSeconds(1), currentCpu: TimeSpan.FromMilliseconds(1500))
            .Should().BeApproximately(50.0 / cores, 1e-9);
        CpuPercent(TimeSpan.FromSeconds(1), startCpu: TimeSpan.FromSeconds(2), currentCpu: TimeSpan.FromSeconds(2))
            .Should().Be(0);

        // Using every core for the whole duration is 100%, and the value never exceeds it.
        CpuPercent(TimeSpan.FromSeconds(1), startCpu: TimeSpan.Zero, currentCpu: TimeSpan.FromSeconds(cores))
            .Should().BeApproximately(100, 1e-9);
        CpuPercent(TimeSpan.FromSeconds(1), startCpu: TimeSpan.Zero, currentCpu: TimeSpan.FromSeconds(10 * cores))
            .Should().Be(100);
    }

    public void CpuUsageIsNotReportedWithoutAPositiveDurationOrWhenTheProcessorTimeWentBackwards()
    {
        CpuPercent(TimeSpan.Zero, startCpu: TimeSpan.FromSeconds(1), currentCpu: TimeSpan.FromSeconds(2))
            .Should().BeNull("no time elapsed");
        CpuPercent(TimeSpan.Zero, startCpu: TimeSpan.FromSeconds(1), currentCpu: TimeSpan.FromSeconds(1))
            .Should().BeNull("no time elapsed");
        CpuPercent(TimeSpan.FromSeconds(-1), startCpu: TimeSpan.FromSeconds(1), currentCpu: TimeSpan.FromSeconds(2))
            .Should().BeNull("the clock went backwards");
        CpuPercent(TimeSpan.FromSeconds(1), startCpu: TimeSpan.FromSeconds(2), currentCpu: TimeSpan.FromSeconds(1))
            .Should().BeNull("the processor time went backwards");
    }

    public void ProcessIoIsOnlyAvailableWhenAllFourCountersAreKnown()
    {
        for (int mask = 0; mask < 16; mask++)
        {
            long? currentRead = (mask & 1) != 0 ? 1100 : null;
            long? currentWrite = (mask & 2) != 0 ? 3200 : null;
            long? startRead = (mask & 4) != 0 ? 100 : null;
            long? startWrite = (mask & 8) != 0 ? 200 : null;

            object artifact = CreateProcessArtifact(
                Path.GetTempPath(),
                TimeSpan.FromSeconds(2),
                startRead: startRead,
                startWrite: startWrite,
                currentRead: currentRead,
                currentWrite: currentWrite);

            GetValue<bool>(artifact, "ProcessIoAvailable").Should().Be(mask == 15, $"the known counters bit mask is {mask}");
        }
    }

    public void ProcessIoRatesAreTheCounterDeltasPerSecond()
    {
        object artifact = CreateProcessArtifact(
            Path.GetTempPath(),
            TimeSpan.FromSeconds(2),
            startRead: 100,
            startWrite: 200,
            currentRead: 1100,
            currentWrite: 3200);

        GetValue<long?>(artifact, "ProcessIoReadBytesDuringTest").Should().Be(1000);
        GetValue<long?>(artifact, "ProcessIoWriteBytesDuringTest").Should().Be(3000);
        GetValue<double?>(artifact, "ProcessIoReadBytesPerSecond").Should().Be(500);
        GetValue<double?>(artifact, "ProcessIoWriteBytesPerSecond").Should().Be(1500);
    }

    public void ProcessIoRatesAreNotReportedWithoutAPositiveDurationOrKnownCounters()
    {
        foreach (TimeSpan elapsed in new[] { TimeSpan.Zero, TimeSpan.FromSeconds(-2) })
        {
            object artifact = CreateProcessArtifact(
                Path.GetTempPath(),
                elapsed,
                startRead: 100,
                startWrite: 200,
                currentRead: 1100,
                currentWrite: 3200);

            GetValue<long?>(artifact, "ProcessIoReadBytesDuringTest").Should().Be(1000, $"the elapsed time is {elapsed}");
            GetValue<long?>(artifact, "ProcessIoWriteBytesDuringTest").Should().Be(3000, $"the elapsed time is {elapsed}");
            GetValue<double?>(artifact, "ProcessIoReadBytesPerSecond").Should().BeNull($"the elapsed time is {elapsed}");
            GetValue<double?>(artifact, "ProcessIoWriteBytesPerSecond").Should().BeNull($"the elapsed time is {elapsed}");
        }

        object readOnly = CreateProcessArtifact(
            Path.GetTempPath(),
            TimeSpan.FromSeconds(2),
            startRead: 100,
            startWrite: 200,
            currentRead: 1100,
            currentWrite: null);
        GetValue<double?>(readOnly, "ProcessIoReadBytesPerSecond").Should().Be(500);
        GetValue<double?>(readOnly, "ProcessIoWriteBytesPerSecond").Should().BeNull();

        object writeOnly = CreateProcessArtifact(
            Path.GetTempPath(),
            TimeSpan.FromSeconds(2),
            startRead: null,
            startWrite: 200,
            currentRead: 1100,
            currentWrite: 3200);
        GetValue<double?>(writeOnly, "ProcessIoReadBytesPerSecond").Should().BeNull();
        GetValue<double?>(writeOnly, "ProcessIoWriteBytesPerSecond").Should().Be(1500);

        object unchanged = CreateProcessArtifact(
            Path.GetTempPath(),
            TimeSpan.FromSeconds(2),
            startRead: 100,
            startWrite: 200,
            currentRead: 100,
            currentWrite: 200);
        GetValue<double?>(unchanged, "ProcessIoReadBytesPerSecond").Should().Be(0);
        GetValue<double?>(unchanged, "ProcessIoWriteBytesPerSecond").Should().Be(0);
    }

    public void CounterDeltasAreOnlyComputedForKnownAndNotDecreasingCounters()
    {
        SubtractCounters(null, null).Should().BeNull();
        SubtractCounters(null, 5).Should().BeNull();
        SubtractCounters(5, null).Should().BeNull();
        SubtractCounters(7, 5).Should().Be(2);
        SubtractCounters(5, 5).Should().Be(0);
        SubtractCounters(0, 0).Should().Be(0);
        SubtractCounters(4, 5).Should().BeNull();
    }

    public void ElapsedTimeIsTheDifferenceBetweenTheTimestamps()
    {
        long second = Stopwatch.Frequency;

        GetElapsed(second, 3 * second).Should().Be(TimeSpan.FromSeconds(2));
        GetElapsed(3 * second, 3 * second).Should().Be(TimeSpan.Zero);
        GetElapsed(5 * second, 3 * second).Should().Be(TimeSpan.FromSeconds(-2));
    }

    public void OutputVolumeUsageIsReportedForAnExistingDirectory()
    {
        using DiagnosticsDirectory directory = new();

        object artifact = CreateProcessArtifact(directory.Path);

        string? error = GetValue<string?>(artifact, "OutputVolumeError");
        string? volumePath = GetValue<string?>(artifact, "OutputVolumePath");
        long? freeBytes = GetValue<long?>(artifact, "OutputVolumeAvailableFreeBytes");
        long? totalBytes = GetValue<long?>(artifact, "OutputVolumeTotalBytes");
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            error.Should().BeNull();
            volumePath.Should().NotBeNullOrEmpty();
            totalBytes.Should().BeGreaterThan(0);
            freeBytes.Should().BeInRange(0, totalBytes!.Value);
        }
        else if (error is null)
        {
            volumePath.Should().NotBeNullOrEmpty();
            freeBytes.Should().BeGreaterThanOrEqualTo(0);
            totalBytes.Should().BeGreaterThanOrEqualTo(0);
        }
        else
        {
            volumePath.Should().BeNull();
            freeBytes.Should().BeNull();
            totalBytes.Should().BeNull();
        }
    }

    public void OutputVolumeFailuresAreReportedInTheArtifactInsteadOfBeingThrown()
    {
        string missingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing");

        object artifact = CreateProcessArtifact(missingDirectory);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            GetValue<string?>(artifact, "OutputVolumeError").Should().NotBeNullOrWhiteSpace();
            GetValue<string?>(artifact, "OutputVolumePath").Should().BeNull();
            GetValue<long?>(artifact, "OutputVolumeAvailableFreeBytes").Should().BeNull();
            GetValue<long?>(artifact, "OutputVolumeTotalBytes").Should().BeNull();
        }
    }

    public void StackFramesCarrySourceFileAndLineInformationWhenSymbolsAreAvailable()
    {
        bool symbolsAvailable = new StackTrace(fNeedFileInfo: true).GetFrame(0)?.GetFileName() is not null;

        FrameInfo[] frames = CaptureFrames();

        string expectedMethod = $"{typeof(TestContextAssertionFailureDiagnosticsWriterMutationTests).FullName}.{nameof(CaptureFrames)}";
        FrameInfo own = frames.Should().ContainSingle(frame => frame.Method == expectedMethod).Which;
        own.Assembly.Should().Be(typeof(TestContextAssertionFailureDiagnosticsWriterMutationTests).Assembly.GetName().Name);
        own.IsFrameworkFrame.Should().BeFalse();
        if (symbolsAvailable)
        {
            own.SourceFile.Should().EndWith(ThisFileName);
            own.Line.Should().BePositive();
        }
    }

#if NET
    public void StackFramesOfMethodsWithoutADeclaringTypeAreReportedAsGlobalMethods()
    {
        // The call is followed by a value load so it is not in tail position: an optimizing JIT (Release) would otherwise
        // turn the call into a tail call and drop this dynamic method's frame from the captured stack.
        var method = new DynamicMethod(
            "InvokeAction",
            typeof(int),
            [typeof(Action)],
            typeof(TestContextAssertionFailureDiagnosticsWriterMutationTests).Module,
            skipVisibility: true);
        ILGenerator il = method.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Callvirt, typeof(Action).GetMethod(nameof(Action.Invoke))!);
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Ret);
        var invoke = (Func<Action, int>)method.CreateDelegate(typeof(Func<Action, int>));

        FrameInfo[] frames = [];
        _ = invoke(() => frames = CaptureFrames());

        frames.Select(frame => frame.Method).Should().Contain("<global>.InvokeAction");
    }
#endif

    private static Type GetNestedType(string name)
        => typeof(TestContextImplementation).GetNestedType(name, BindingFlags.NonPublic | BindingFlags.Public)!;

    private static MethodInfo GetStaticMethod(string name)
        => typeof(TestContextImplementation).GetMethod(name, StaticMembers)!;

    private static T GetValue<T>(object instance, string propertyName)
        => (T)instance.GetType().GetProperty(propertyName, InstanceMembers)!.GetValue(instance)!;

    private static TestContextImplementation CreateContext(string resultsDirectory)
    {
        var method = new Mock<ITestMethod>();
        method.SetupGet(value => value.FullClassName).Returns("Tests.Writer");
        method.SetupGet(value => value.Name).Returns("Runs");
        return new TestContextImplementation(
            method.Object, "Tests.Writer", new Dictionary<string, object?> { ["TestResultsDirectory"] = resultsDirectory }, null, null);
    }

    private static object CreateActiveTest(TestContextImplementation context, long id, string className, long startTimestamp)
        => Activator.CreateInstance(
            ActiveTestType,
            InstanceMembers,
            null,
            [id, context, className, "Runs", null, 1, DateTimeOffset.UtcNow, startTimestamp, CreateBaseline(TimeSpan.Zero, null, null)],
            null)!;

    private static object CreateBaseline(TimeSpan processorTime, long? readBytes, long? writeBytes)
        => Activator.CreateInstance(ResourceBaselineType, processorTime, readBytes, writeBytes)!;

    private static object CreateSnapshot(TimeSpan processorTime, long? readBytes, long? writeBytes)
        => Activator.CreateInstance(
            ResourceSnapshotType,
            InstanceMembers,
            null,
            [4242, "writer-test-process", processorTime, 1L, 2L, 3L, (long?)4L, (long?)5L, readBytes, writeBytes],
            null)!;

    private static object CreateProcessArtifact(
        string outputDirectory,
        TimeSpan? elapsed = null,
        TimeSpan? startCpu = null,
        TimeSpan? currentCpu = null,
        long? startRead = null,
        long? startWrite = null,
        long? currentRead = null,
        long? currentWrite = null)
        => CreateProcessArtifactMethod.Invoke(
            null,
            [
                CreateBaseline(startCpu ?? TimeSpan.Zero, startRead, startWrite),
                CreateSnapshot(currentCpu ?? TimeSpan.Zero, currentRead, currentWrite),
                elapsed ?? TimeSpan.FromSeconds(1),
                outputDirectory,
            ])!;

    private static double? CpuPercent(TimeSpan elapsed, TimeSpan startCpu, TimeSpan currentCpu)
        => GetValue<double?>(CreateProcessArtifact(Path.GetTempPath(), elapsed, startCpu, currentCpu), "CpuPercentDuringTest");

    private static long? SubtractCounters(long? current, long? start)
        => (long?)SubtractCountersMethod.Invoke(null, [current, start]);

    private static TimeSpan GetElapsed(long startTimestamp, long endTimestamp)
        => (TimeSpan)GetElapsedMethod.Invoke(null, [startTimestamp, endTimestamp])!;

    private static JsonDocument WriteArtifact(object activeTest, string message, string? expected, string? actual)
    {
        string path = (string)WriteMethod.Invoke(null, [activeTest, 1, message, expected, actual])!;
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static FrameInfo[] CaptureFrames()
    {
        List<FrameInfo> frames = [];
        foreach (object frame in (Array)CaptureStackFramesMethod.Invoke(null, null)!)
        {
            frames.Add(new FrameInfo(
                GetValue<string?>(frame, "Method"),
                GetValue<string?>(frame, "Assembly"),
                GetValue<string?>(frame, "File"),
                GetValue<int>(frame, "Line"),
                GetValue<bool>(frame, "IsFrameworkFrame")));
        }

        return [.. frames];
    }

    private static ActiveTestRegistration RegisterActiveTests(params object[] activeTests)
        => new(activeTests);

    private readonly record struct FrameInfo(string? Method, string? Assembly, string? SourceFile, int Line, bool IsFrameworkFrame);

    private sealed class ActiveTestRegistration : IDisposable
    {
        private readonly List<long> _ids = [];

        public ActiveTestRegistration(object[] activeTests)
        {
            try
            {
                foreach (object activeTest in activeTests)
                {
                    long id = GetValue<long>(activeTest, "Id");
                    ActiveTests.Add(id, activeTest);
                    _ids.Add(id);
                }
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            foreach (long id in _ids)
            {
                ActiveTests.Remove(id);
            }

            _ids.Clear();
        }
    }

    private sealed class DiagnosticsDirectory : IDisposable
    {
        public DiagnosticsDirectory() => Path = Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "d" + Guid.NewGuid().ToString("N").Substring(0, 12))).FullName;

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
#endif
