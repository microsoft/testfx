// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP && !WIN_UI
using System.Runtime.Serialization;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

internal sealed partial class TestContextImplementation
{
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

        [DataMember(Name = "currentCulture", Order = 5)]
        public string CurrentCulture { get; set; } = null!;

        [DataMember(Name = "currentUICulture", Order = 6)]
        public string CurrentUICulture { get; set; } = null!;

        [DataMember(Name = "processorCount", Order = 7)]
        public int ProcessorCount { get; set; }

        [DataMember(Name = "cpuPercentDuringTest", Order = 8, EmitDefaultValue = false)]
        public double? CpuPercentDuringTest { get; set; }

        [DataMember(Name = "totalProcessorTimeMilliseconds", Order = 9)]
        public double TotalProcessorTimeMilliseconds { get; set; }

        [DataMember(Name = "workingSetBytes", Order = 10)]
        public long WorkingSetBytes { get; set; }

        [DataMember(Name = "privateMemoryBytes", Order = 11)]
        public long PrivateMemoryBytes { get; set; }

        [DataMember(Name = "managedHeapBytes", Order = 12)]
        public long ManagedHeapBytes { get; set; }

        [DataMember(Name = "gcMemoryLoadBytes", Order = 13, EmitDefaultValue = false)]
        public long? GcMemoryLoadBytes { get; set; }

        [DataMember(Name = "gcTotalAvailableMemoryBytes", Order = 14, EmitDefaultValue = false)]
        public long? GcTotalAvailableMemoryBytes { get; set; }

        [DataMember(Name = "processIoAvailable", Order = 15)]
        public bool ProcessIoAvailable { get; set; }

        [DataMember(Name = "processIoReadBytesDuringTest", Order = 16, EmitDefaultValue = false)]
        public long? ProcessIoReadBytesDuringTest { get; set; }

        [DataMember(Name = "processIoWriteBytesDuringTest", Order = 17, EmitDefaultValue = false)]
        public long? ProcessIoWriteBytesDuringTest { get; set; }

        [DataMember(Name = "processIoReadBytesPerSecond", Order = 18, EmitDefaultValue = false)]
        public double? ProcessIoReadBytesPerSecond { get; set; }

        [DataMember(Name = "processIoWriteBytesPerSecond", Order = 19, EmitDefaultValue = false)]
        public double? ProcessIoWriteBytesPerSecond { get; set; }

        [DataMember(Name = "outputVolumePath", Order = 20, EmitDefaultValue = false)]
        public string? OutputVolumePath { get; set; }

        [DataMember(Name = "outputVolumeAvailableFreeBytes", Order = 21, EmitDefaultValue = false)]
        public long? OutputVolumeAvailableFreeBytes { get; set; }

        [DataMember(Name = "outputVolumeTotalBytes", Order = 22, EmitDefaultValue = false)]
        public long? OutputVolumeTotalBytes { get; set; }

        [DataMember(Name = "outputVolumeError", Order = 23, EmitDefaultValue = false)]
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
}
#endif
