// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP && !WIN_UI

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

/// <summary>
/// Cleanup and retention policy of the per-test temporary directory of
/// <see cref="TestContextImplementation"/>.
/// </summary>
internal sealed partial class TestContextImplementation
{
    /// <summary>
    /// Environment variable that, when set to a truthy value, retains every per-test temporary
    /// directory (including those of passing tests) instead of deleting them. Used for debugging.
    /// </summary>
    private const string RetainTestTempDirectoryEnvironmentVariable = "MSTEST_TEST_TEMP_DIRECTORY_RETAIN";

    /// <summary>
    /// Deletes the per-test temporary directory unless the test failed or retention was requested.
    /// Best-effort: it swallows all exceptions, so a passing test cannot be failed by a cleanup
    /// error. It runs after the test has completed, so it cannot extend the test's own execution or
    /// trip its timeout; note that the delete is synchronous, so exception swallowing is guaranteed
    /// but bounded cleanup time is not (a pathologically stalled filesystem could make disposal
    /// itself slow).
    /// </summary>
    private void CleanupTestTempDirectory()
    {
        // Read the lazy-init state under the same lock the getter writes it under. Without this
        // acquire barrier, a directory created on a worker thread the test spawned (but did not
        // join) could be observed as not-yet-created here, silently skipping cleanup and leaking
        // the directory even on a passing test.
        string? directory;
        lock (_testTempDirectoryLock)
        {
            // Mark cleanup as started while holding the lock so a concurrent first getter that has
            // not yet created the directory will see this and skip creation, instead of creating a
            // directory after cleanup has already run (which would leak it).
            _testTempDirectoryCleanupStarted = true;

            if (!_testTempDirectoryCreated || _testTempDirectory is not { Length: > 0 } createdDirectory)
            {
                return;
            }

            directory = createdDirectory;
        }

        if (ShouldRetainTestTempDirectory())
        {
            return;
        }

        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (Exception ex)
        {
            // A leaked file handle, or a transient antivirus/indexer lock on Windows, can make the
            // delete fail. This must never fail an otherwise passing test, so we swallow it and only
            // surface the failure through the diagnostic trace log. The logging itself is guarded so
            // that a misbehaving trace logger cannot let an exception escape Dispose either.
            try
            {
                PlatformServiceProvider.Instance.AdapterTraceLogger.Warning(
                    "Failed to delete per-test temporary directory '{0}': {1}", directory, ex);
            }
            catch (Exception)
            {
                // Intentionally ignored: cleanup is best-effort and must never throw from Dispose.
            }
        }
    }

    /// <summary>
    /// Determines whether the per-test temporary directory should be kept: retained on any
    /// non-passing outcome, when the test registered a result file living inside it, or when
    /// retention is forced via the environment variable escape hatch.
    /// </summary>
    private bool ShouldRetainTestTempDirectory()
    {
        // Retain a failed (or otherwise non-passing) test's artifacts for inspection.
        if (_outcome != UnitTestOutcome.Passed)
        {
            return true;
        }

        // If the test registered a result file (via AddResultFile) that lives inside the temp
        // directory, that file is referenced as a result attachment and is collected by the test
        // host *after* this context is disposed. Deleting the directory now would leave the
        // attachment pointing at a missing file, so retain it in that case.
        if (_hasResultFileUnderTestTempDirectory)
        {
            return true;
        }

        string? retain;
        try
        {
            retain = Environment.GetEnvironmentVariable(RetainTestTempDirectoryEnvironmentVariable);
        }
        catch (System.Security.SecurityException)
        {
            // Environment access is restricted (possible on .NET Framework). Treat the retention
            // override as unset so cleanup does not throw out of Dispose.
            return false;
        }

        return retain is "1" || string.Equals(retain, "true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns whether <paramref name="filePath"/> is located inside <paramref name="directory"/>.
    /// </summary>
    private static bool IsPathUnderDirectory(string filePath, string directory)
    {
        string normalizedDirectory = Path.GetFullPath(directory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string normalizedFile = Path.GetFullPath(filePath);
        return normalizedFile.StartsWith(
            normalizedDirectory,
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    /// <summary>
    /// Returns whether any currently-registered result file lives inside the per-test temporary
    /// directory.
    /// </summary>
    private bool HasResultFileUnderTestTempDirectory()
    {
        if (_testResultFiles is not { Count: > 0 } files
            || !Volatile.Read(ref _testTempDirectoryCreated)
            || _testTempDirectory is not { Length: > 0 } tempDir)
        {
            return false;
        }

        foreach (string file in files)
        {
            if (IsPathUnderDirectory(file, tempDir))
            {
                return true;
            }
        }

        return false;
    }
}

#endif
