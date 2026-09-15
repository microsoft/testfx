// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP && !WIN_UI
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
#endif

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

internal sealed partial class TestContextImplementation
{
#if !WINDOWS_UWP && !WIN_UI
    private static readonly ConcurrentDictionary<long, ActiveTest> ActiveTests = new();
    private static readonly AsyncLocal<ActiveTest?> CurrentActiveTest = new();

    private static long s_nextActiveTestId;

    private AssertionFailureCaptureBudget _assertionFailureCaptureBudget = new();
    private ConcurrentQueue<string>? _completedAssertionFailureDiagnosticsPaths;
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
    internal void FinalizeAssertionFailureDiagnosticsExecution(TestResult[] results, bool resetCaptureBudget)
    {
        try
        {
            ConcurrentQueue<string>? completedPaths = Interlocked.Exchange(ref _completedAssertionFailureDiagnosticsPaths, null);
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
            List<string>? mutableTargetFiles = null;
            foreach (string path in paths)
            {
                if (existingPaths.Add(path))
                {
                    mutableTargetFiles ??= targetResult.ResultFiles is null ? [] : [.. targetResult.ResultFiles];
                    mutableTargetFiles.Add(path);
                }
            }

            if (mutableTargetFiles is not null)
            {
                targetResult.ResultFiles = mutableTargetFiles;
            }

            Volatile.Write(ref _retainAssertionFailureDiagnosticsDirectory, 1);
        }
        finally
        {
            if (resetCaptureBudget)
            {
                _assertionFailureCaptureBudget.Reset();
            }
        }
    }

    internal void TransferAssertionFailureDiagnosticsTo(TestContextImplementation destination)
    {
        ConcurrentQueue<string>? completedPaths = Interlocked.Exchange(ref _completedAssertionFailureDiagnosticsPaths, null);
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
#endif
}
