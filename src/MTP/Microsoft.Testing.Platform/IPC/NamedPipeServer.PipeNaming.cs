// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.IPC;

internal sealed partial class NamedPipeServer
{
    /// <summary>
    /// Computes the OS-level named pipe name for a friendly <paramref name="name"/>.
    /// </summary>
    /// <remarks>
    /// Invariant (important for cross-version / cross-repo compatibility): the process that <b>creates</b> the
    /// pipe resolves the directory locally and hands the <b>fully-resolved</b> path to the peer (via an
    /// environment variable, a command-line argument, or the dotnet-test handshake). Peers use that path verbatim
    /// and never recompute it. Do NOT turn the directory into a convention that both sides derive independently
    /// from a shared friendly name/env var - doing so would couple the SDK and test-host versions. Because only
    /// the creator's resolution is ever used, a difference in TESTINGPLATFORM_PIPE_DIRECTORY / TMPDIR between the
    /// two processes is harmless.
    /// </remarks>
    public static PipeNameDescription GetPipeName(string name)
    {
        if (!IsUnix)
        {
            return new PipeNameDescription($"testingplatform.pipe.{name.Replace('\\', '.')}");
        }

        // On Unix the named pipe is backed by a Unix domain socket file on disk. Historically this file
        // was always created under '/tmp' (similar to
        // https://github.com/dotnet/roslyn/blob/99bf83c7bc52fa1ff27cf792db38755d5767c004/src/Compilers/Shared/NamedPipeUtil.cs#L26-L42).
        // That is a problem in sandboxed environments that block '/tmp' (see
        // https://github.com/microsoft/testfx/issues/9821), so we allow the directory to be relocated.
        // Resolution precedence:
        //   1. TESTINGPLATFORM_PIPE_DIRECTORY   - explicit opt-in override, a guaranteed escape hatch.
        //   2. Path.GetTempPath()               - honors TMPDIR on Unix; usually a per-user, allowed dir.
        //   3. '/tmp'                           - preserves the previous default when neither is set.
        (string directory, bool isExplicitOverride) = ResolvePipeDirectory();

        // Normalize to an absolute path regardless of which precedence branch supplied the directory. An
        // explicit override or a relative TMPDIR can be relative, and on Unix NamedPipeServerStream only treats
        // rooted names as socket paths (it rejects separators in non-rooted names). The invariant also requires
        // handing peers a fully-resolved path. This additionally collapses any '..' segments. '/tmp' and an
        // already-absolute temp path are unchanged.
        directory = Path.GetFullPath(directory);

        // Only actively validate the explicit override: it is user-supplied and the most likely to be wrong
        // (typo, missing directory, wrong permissions), so we create it if needed and fail fast with an
        // actionable message. Path.GetTempPath()/'/tmp' are OS-managed and effectively always writable, so we
        // skip the probe there to avoid extra I/O and a behavior change on every pipe creation.
        if (isExplicitOverride)
        {
            EnsureDirectoryIsWritable(directory);
        }

        string path = Path.Combine(directory, name);
        EnsurePathLengthWithinLimit(path);

        return new PipeNameDescription(path);
    }

    private static (string Directory, bool IsExplicitOverride) ResolvePipeDirectory()
        => ResolvePipeDirectory(
            // Read the environment variable directly rather than through IEnvironment: GetPipeName is a static
            // method invoked before any NamedPipeServer instance (and its IEnvironment) exists, and this file is
            // shared-compiled into extension projects that do not link the SystemEnvironment wrapper. The banned
            // API is suppressed locally, mirroring how SystemEnvironment itself wraps Environment.
#pragma warning disable RS0030 // Do not use banned APIs
            Environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_PIPE_DIRECTORY),
#pragma warning restore RS0030 // Do not use banned APIs
            Path.GetTempPath());

    // Pure resolution logic split out so it can be unit-tested on any OS (the Unix branch of GetPipeName
    // never runs on Windows) without mutating process-wide environment variables.
    internal static (string Directory, bool IsExplicitOverride) ResolvePipeDirectory(string? overrideDirectory, string? tempPath)
    {
        if (!RoslynString.IsNullOrWhiteSpace(overrideDirectory))
        {
            return (overrideDirectory, true);
        }

        // Path.GetTempPath() honors TMPDIR on Unix and already falls back to '/tmp' itself when TMPDIR is unset,
        // and it always returns a non-empty string in practice. The explicit '/tmp' below is therefore a
        // defensive net that is only reachable when a caller passes null/empty tempPath (i.e. the test overload).
        return RoslynString.IsNullOrWhiteSpace(tempPath)
            ? ("/tmp", false)
            : (tempPath, false);
    }

    // Internal for unit testing: normalize/create the explicit override directory and verify it is writable,
    // failing fast with an actionable, localized message instead of a cryptic socket bind error later.
    internal static void EnsureDirectoryIsWritable(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);

            // Probe write access with a short-lived file so a misconfigured directory fails fast with an
            // actionable message instead of surfacing a cryptic socket bind failure later on.
            string probePath = Path.Combine(directory, $"testingplatform.probe.{Guid.NewGuid():N}");
            using (File.Create(probePath))
            {
            }

            File.Delete(probePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or NotSupportedException or ArgumentException)
        {
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    PlatformResources.NamedPipeDirectoryNotWritableErrorMessage,
                    directory,
                    ex.Message,
                    EnvironmentVariableConstants.TESTINGPLATFORM_PIPE_DIRECTORY),
                ex);
        }
    }

    // Internal for unit testing: enforce the Unix domain socket sun_path budget so a too-long directory
    // (e.g. a deep TMPDIR on macOS) fails with an actionable message instead of a cryptic bind error.
    internal static void EnsurePathLengthWithinLimit(string path)
    {
        int byteLength = System.Text.Encoding.UTF8.GetByteCount(path);
        if (byteLength > MaxUnixDomainSocketPathLengthInBytes)
        {
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    PlatformResources.NamedPipePathTooLongErrorMessage,
                    path,
                    byteLength,
                    MaxUnixDomainSocketPathLengthInBytes,
                    EnvironmentVariableConstants.TESTINGPLATFORM_PIPE_DIRECTORY));
        }
    }
}
