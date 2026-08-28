// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP && !WIN_UI

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

/// <summary>
/// Per-test temporary directory state and creation for <see cref="TestContextImplementation"/>.
/// </summary>
internal sealed partial class TestContextImplementation
{
    /// <summary>
    /// Maximum length (in characters) of the readable, sanitized test-name portion of the
    /// per-test temporary directory name. This is a <em>cap</em>: the actual budget is computed
    /// adaptively from how much room the base path leaves (see <see cref="CreateTestTempDirectory"/>),
    /// and is only allowed to grow up to this cap.
    /// </summary>
    private const int TestTempDirectoryNameMaxLength = 50;

    /// <summary>
    /// Minimum readable-name budget worth keeping. If the base path is so deep that the adaptive
    /// budget for the readable portion would drop below this floor, the implementation falls back to
    /// the system temporary directory (which is short) rather than emit a barely-readable name that
    /// still risks overflowing <c>MAX_PATH</c>.
    /// </summary>
    private const int TestTempDirectoryNameMinLength = 8;

    private const uint TestTempDirectoryUnixCreateMode = 0x1C0;

    [DllImport("libc", EntryPoint = "mkdir", SetLastError = true)]
    private static extern int MkDir([In] byte[] path, uint mode);

    [DllImport("libc", EntryPoint = "chmod", SetLastError = true)]
    private static extern int ChMod([In] byte[] path, uint mode);

    /// <summary>
    /// Guards lazy creation of <see cref="_testTempDirectory"/>.
    /// </summary>
#if NET9_0_OR_GREATER
    private readonly Lock _testTempDirectoryLock = new();
#else
    private readonly object _testTempDirectoryLock = new();
#endif

    /// <summary>
    /// Whether this context represents an executing test (as opposed to an assembly/class
    /// initialize or cleanup fixture context). <see cref="TestTempDirectory"/> is a *per-test*
    /// scratch directory; fixture contexts are not per-test and are not always disposed (e.g.
    /// <c>ClassCleanupManager.ForceCleanup</c> contexts), so creating a directory for them would
    /// leak it. The getter returns <see langword="null"/> when this is <see langword="false"/>.
    /// </summary>
    private bool _isTestExecutionContext;

    /// <summary>
    /// The lazily-created per-test temporary directory, or <see langword="null"/> if it has not
    /// been accessed (and therefore not created) yet.
    /// </summary>
    private string? _testTempDirectory;

    /// <summary>
    /// Whether <see cref="_testTempDirectory"/> has been created.
    /// </summary>
    private bool _testTempDirectoryCreated;

    /// <summary>
    /// Whether the test registered (via <see cref="AddResultFile"/>) a result file that lives inside
    /// the per-test temporary directory. Such a file is reported to the host as a result attachment
    /// and is collected after this context is disposed, so the directory must be retained even on a
    /// passing outcome. This flag is set eagerly in <see cref="AddResultFile"/> because the framework
    /// consumes the result-file list during execution, before cleanup runs.
    /// </summary>
    private bool _hasResultFileUnderTestTempDirectory;

    /// <summary>
    /// Whether cleanup of the per-test temporary directory has started (i.e. the context is being
    /// or has been disposed). Once set, the getter must not create a new directory, otherwise a
    /// late access from a background thread the test spawned could create a directory *after*
    /// cleanup already ran, leaking it. Guarded by <see cref="_testTempDirectoryLock"/>.
    /// </summary>
    private bool _testTempDirectoryCleanupStarted;

    /// <inheritdoc/>
    public override string? TestTempDirectory
    {
        get
        {
            // A per-test scratch directory only makes sense for an executing test. Fixture
            // (assembly/class initialize and cleanup) contexts are not per-test and may never be
            // disposed, so creating a directory for them would leak it — return null instead.
            if (!_isTestExecutionContext)
            {
                return null;
            }

            if (Volatile.Read(ref _testTempDirectoryCreated))
            {
                return _testTempDirectory;
            }

            lock (_testTempDirectoryLock)
            {
                if (!_testTempDirectoryCreated)
                {
                    if (_testTempDirectoryCleanupStarted)
                    {
                        // The context is being (or has been) disposed. Creating a directory now
                        // would leak it, because cleanup has already inspected the state. Treat a
                        // post-cleanup access as a no-op and return null (the test has finished).
                        return null;
                    }

                    _testTempDirectory = CreateTestTempDirectory();
                    Volatile.Write(ref _testTempDirectoryCreated, true);
                }
            }

            return _testTempDirectory;
        }
    }

    /// <summary>
    /// Creates the per-test temporary directory. The directory lives under the run's results
    /// directory (so it is discoverable next to other run output); when no results directory is
    /// configured this is the test assembly's output directory. On Windows the readable-name budget
    /// is sized adaptively from how much room the base path leaves under <c>MAX_PATH</c>, and — when
    /// the results directory is so deep that even a minimal readable name cannot preserve the
    /// reserved headroom for the test's own files — the implementation falls back to the short
    /// system temporary directory instead. It also falls back to the system temporary directory when
    /// the chosen base directory cannot be written to (for example a read-only output directory), so
    /// the property returns a usable path rather than throwing from the getter.
    /// </summary>
    private string CreateTestTempDirectory()
    {
        string? resultsDirectory = TestResultsDirectory is { Length: > 0 } results ? results : null;
        string baseDirectory = resultsDirectory ?? Path.GetTempPath();
        bool baseIsTemp = resultsDirectory is null;

        // Size the readable-name budget so that base + '\' + name + '_' + suffix, plus the reserved
        // headroom for the files the test writes inside, stays within MAX_PATH on Windows. On other
        // operating systems path length is effectively a non-issue (per-component limit is 255 and
        // our whole segment is well under that), so the readable name simply gets the full cap.
        int nameBudget = TestTempDirectoryNameMaxLength;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            int available = ComputeReadableNameBudget(baseDirectory);
            if (available < TestTempDirectoryNameMinLength && !baseIsTemp)
            {
                // The results directory is too deep to leave usable headroom; fall back to the
                // short system temp directory so the test still gets room to write.
                baseDirectory = Path.GetTempPath();
                baseIsTemp = true;
                available = ComputeReadableNameBudget(baseDirectory);
            }

            nameBudget = available < 0 ? 0 : Math.Min(available, TestTempDirectoryNameMaxLength);
        }

        if (TryCreateTestTempDirectoryUnder(baseDirectory, nameBudget, out string created))
        {
            return created;
        }

        // The chosen base directory could not be written to (e.g. a read-only output directory).
        // Fall back to the system temporary directory, which is writable, so the property still
        // returns a usable path instead of throwing from the getter.
        if (!baseIsTemp)
        {
            string tempBase = Path.GetTempPath();
            int tempBudget = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? Math.Max(0, Math.Min(ComputeReadableNameBudget(tempBase), TestTempDirectoryNameMaxLength))
                : TestTempDirectoryNameMaxLength;
            if (TryCreateTestTempDirectoryUnder(tempBase, tempBudget, out created))
            {
                return created;
            }
        }

        // Could not create anywhere with a readable name; make one last attempt under the system
        // temp directory and let any exception surface as a genuine error.
        string fallback = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        CreateDirectoryWithRestrictedPermissions(fallback);
        return fallback;
    }

    /// <summary>
    /// Attempts to create a uniquely-named per-test temporary directory under <paramref name="baseDirectory"/>.
    /// Returns <see langword="false"/> (rather than throwing) when the base directory cannot be
    /// written to, so the caller can fall back to another location.
    /// </summary>
    private bool TryCreateTestTempDirectoryUnder(string baseDirectory, int nameBudget, out string createdPath)
    {
        string namePart = SanitizeTestTempDirectoryName(GetTestTempDirectoryNameSource(), nameBudget);

        try
        {
            Directory.CreateDirectory(baseDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            createdPath = string.Empty;
            return false;
        }

        // The suffix is a full 128-bit GUID, so two contexts choosing the same directory name is
        // cryptographically negligible. Exists + CreateDirectory is not an atomic exclusive create,
        // so the retry loop below is a belt-and-braces guard rather than a real necessity.
        const int maxAttempts = 10;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            string suffix = Guid.NewGuid().ToString("N").Substring(0, TestTempDirectoryUniqueSuffixLength);
            string candidateName = namePart.Length == 0 ? suffix : $"{namePart}_{suffix}";
            string candidate = Path.Combine(baseDirectory, candidateName);
            if (Directory.Exists(candidate))
            {
                continue;
            }

            try
            {
                CreateDirectoryWithRestrictedPermissions(candidate);
                createdPath = candidate;
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                // The base directory is not writable — a read-only output directory, or (on .NET
                // Framework) a denied filesystem permission surfaced as SecurityException. Signal
                // failure so the caller can fall back to the system temporary directory. Retrying a
                // different name under the same base would not help, so bail out immediately.
                createdPath = string.Empty;
                return false;
            }
        }

        createdPath = string.Empty;
        return false;
    }

    private static void CreateDirectoryWithRestrictedPermissions(string path)
    {
#if NETCOREAPP
        if (OperatingSystem.IsBrowser() || OperatingSystem.IsWasi())
        {
            Directory.CreateDirectory(path);
            return;
        }
#endif

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            byte[] nullTerminatedUtf8Path = System.Text.Encoding.UTF8.GetBytes(path + "\0");
            int result = MkDir(nullTerminatedUtf8Path, TestTempDirectoryUnixCreateMode);
            if (result != 0)
            {
                int error = Marshal.GetLastWin32Error();
                throw new IOException($"Could not create test temporary directory '{path}'.", new System.ComponentModel.Win32Exception(error));
            }

            result = ChMod(nullTerminatedUtf8Path, TestTempDirectoryUnixCreateMode);
            if (result != 0)
            {
                int error = Marshal.GetLastWin32Error();
                var permissionException = new System.ComponentModel.Win32Exception(error);
                try
                {
                    Directory.Delete(path);
                }
                catch (Exception cleanupException) when (cleanupException is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                    throw new IOException(
                        $"Could not set permissions on or remove test temporary directory '{path}'.",
                        new AggregateException(permissionException, cleanupException));
                }

                throw new IOException($"Could not set permissions on test temporary directory '{path}'.", permissionException);
            }

            return;
        }

        Directory.CreateDirectory(path);
    }
}

#endif
