// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET
using System.Runtime.InteropServices;
#endif

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Utility class for creating memory dumps on assertion failure.
/// </summary>
internal static class DumpUtility
{
    private static readonly object Lock = new();
    private static bool s_dumpAttempted;

    /// <summary>
    /// Attempts to create a memory dump of the current process.
    /// </summary>
    /// <param name="dumpDirectory">The directory where the dump should be written.</param>
    /// <returns>True if the dump was created successfully; otherwise, false.</returns>
    public static bool TryCaptureDump(string? dumpDirectory)
    {
        // Prevent multiple dumps from being created for cascading assertion failures
        lock (Lock)
        {
            if (s_dumpAttempted)
            {
                return false;
            }

            s_dumpAttempted = true;
        }

        try
        {
            string effectiveDir = string.IsNullOrEmpty(dumpDirectory)
                ? Path.GetTempPath()
                : dumpDirectory!;

            string dumpFileName = Path.Combine(
                effectiveDir,
                $"MSTest_AssertionFailure_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Environment.ProcessId}.dmp");

            // Ensure the directory exists
            if (!Directory.Exists(effectiveDir))
            {
                Directory.CreateDirectory(effectiveDir);
            }

#if NET
            return TryCaptureDumpUsingDiagnosticsClient(dumpFileName);
#elif NETFRAMEWORK
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                return TryCaptureDumpUsingMiniDumpWriteDump(dumpFileName);
            }

            return false;
#else
            // netstandard2.0 - try reflection-based approach
            return TryCaptureDumpViaReflection(dumpFileName);
#endif
        }
        catch
        {
            // Dump creation should never cause tests to fail
            return false;
        }
    }

#if NET
    [UnsupportedOSPlatform("browser")]
    private static bool TryCaptureDumpUsingDiagnosticsClient(string dumpFileName)
    {
        try
        {
            // Use reflection to avoid a hard dependency on Microsoft.Diagnostics.NETCore.Client
            // The package may or may not be available at runtime
            Assembly? diagnosticsAssembly = null;

            try
            {
                diagnosticsAssembly = Assembly.Load("Microsoft.Diagnostics.NETCore.Client");
            }
            catch (FileNotFoundException)
            {
                // Assembly not available, fall back to Windows native API if applicable
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return TryCaptureDumpUsingMiniDumpWriteDump(dumpFileName);
                }

                return false;
            }

            Type? diagnosticsClientType = diagnosticsAssembly?.GetType("Microsoft.Diagnostics.NETCore.Client.DiagnosticsClient");
            Type? dumpTypeEnum = diagnosticsAssembly?.GetType("Microsoft.Diagnostics.NETCore.Client.DumpType");

            if (diagnosticsClientType is null || dumpTypeEnum is null)
            {
                return false;
            }

            // Create DiagnosticsClient instance for current process
            object? client = Activator.CreateInstance(diagnosticsClientType, Environment.ProcessId);
            if (client is null)
            {
                return false;
            }

            // Get DumpType.WithHeap value (provides good balance of info vs size)
            object? dumpTypeValue = Enum.Parse(dumpTypeEnum, "WithHeap");

            // Call WriteDump(DumpType dumpType, string path, bool logDumpGeneration)
            MethodInfo? writeDumpMethod = diagnosticsClientType.GetMethod(
                "WriteDump",
                [dumpTypeEnum, typeof(string), typeof(bool)]);

            writeDumpMethod?.Invoke(client, [dumpTypeValue, dumpFileName, false]);

            return File.Exists(dumpFileName);
        }
        catch
        {
            return false;
        }
    }
#endif

#if NETFRAMEWORK || NET
    [SupportedOSPlatform("windows")]
    private static bool TryCaptureDumpUsingMiniDumpWriteDump(string dumpFileName)
    {
        try
        {
            using var currentProcess = Process.GetCurrentProcess();
            using var fileStream = new FileStream(dumpFileName, FileMode.Create, FileAccess.ReadWrite, FileShare.None);

            MinidumpExceptionInformation exceptionInfo = default;

            // Use MiniDumpWithFullMemory for comprehensive debugging info
            const MinidumpType dumpType =
                MinidumpType.MiniDumpWithFullMemory
                | MinidumpType.MiniDumpWithDataSegs
                | MinidumpType.MiniDumpWithHandleData
                | MinidumpType.MiniDumpWithUnloadedModules
                | MinidumpType.MiniDumpWithFullMemoryInfo
                | MinidumpType.MiniDumpWithThreadInfo;

            bool success = NativeMethods.MiniDumpWriteDump(
                currentProcess.Handle,
                (uint)currentProcess.Id,
                fileStream.SafeFileHandle,
                dumpType,
                ref exceptionInfo,
                IntPtr.Zero,
                IntPtr.Zero);

            return success && File.Exists(dumpFileName);
        }
        catch
        {
            return false;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private readonly struct MinidumpExceptionInformation
    {
        public readonly uint ThreadId;
        public readonly IntPtr ExceptionPointers;
        public readonly int ClientPointers;
    }

    [Flags]
    private enum MinidumpType : uint
    {
        MiniDumpNormal = 0,
        MiniDumpWithDataSegs = 1 << 0,
        MiniDumpWithFullMemory = 1 << 1,
        MiniDumpWithHandleData = 1 << 2,
        MiniDumpWithUnloadedModules = 1 << 5,
        MiniDumpWithFullMemoryInfo = 1 << 11,
        MiniDumpWithThreadInfo = 1 << 12,
    }

    private static class NativeMethods
    {
        [DllImport("Dbghelp.dll", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern bool MiniDumpWriteDump(
            IntPtr hProcess,
            uint processId,
            Microsoft.Win32.SafeHandles.SafeFileHandle hFile,
            MinidumpType dumpType,
            ref MinidumpExceptionInformation exceptionParam,
            IntPtr userStreamParam,
            IntPtr callbackParam);
    }
#endif

#if NETSTANDARD2_0
    private static bool TryCaptureDumpViaReflection(string dumpFileName)
    {
        try
        {
            // Try to load DiagnosticsClient via reflection
            Assembly? diagnosticsAssembly = null;

            try
            {
                diagnosticsAssembly = Assembly.Load("Microsoft.Diagnostics.NETCore.Client");
            }
            catch (FileNotFoundException)
            {
                return false;
            }

            Type? diagnosticsClientType = diagnosticsAssembly?.GetType("Microsoft.Diagnostics.NETCore.Client.DiagnosticsClient");
            Type? dumpTypeEnum = diagnosticsAssembly?.GetType("Microsoft.Diagnostics.NETCore.Client.DumpType");

            if (diagnosticsClientType is null || dumpTypeEnum is null)
            {
                return false;
            }

            // Create DiagnosticsClient instance for current process
            int processId = Process.GetCurrentProcess().Id;
            object? client = Activator.CreateInstance(diagnosticsClientType, processId);
            if (client is null)
            {
                return false;
            }

            // Get DumpType.WithHeap value
            object? dumpTypeValue = Enum.Parse(dumpTypeEnum, "WithHeap");

            // Call WriteDump(DumpType dumpType, string path, bool logDumpGeneration)
            MethodInfo? writeDumpMethod = diagnosticsClientType.GetMethod(
                "WriteDump",
                new[] { dumpTypeEnum, typeof(string), typeof(bool) });

            writeDumpMethod?.Invoke(client, new[] { dumpTypeValue, dumpFileName, false });

            return File.Exists(dumpFileName);
        }
        catch
        {
            return false;
        }
    }
#endif
}
