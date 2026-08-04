// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Extensions;

/// <summary>
/// Resolves the first user source location (workspace-relative file path + line) from an exception stack
/// trace, so a failure can be annotated on the reporting host (Azure DevOps <c>##vso[task.logissue]</c> or
/// GitHub Actions <c>::error</c>). Shared by the Azure DevOps and GitHub Actions reporters.
/// </summary>
internal static class StackTraceSourceLocationResolver
{
    // Source-linked (deterministic) builds emit paths rooted at '/_/' instead of the original absolute path.
    private const string DeterministicBuildRoot = "/_/";

    private static readonly char[] NewlineCharacters = ['\r', '\n'];

    // Path containment must follow the running platform's filesystem semantics. Windows paths compare
    // case-insensitively, but elsewhere '/tmp/Repo' and '/tmp/repo' are distinct directories, so an
    // ignore-case containment test would accept a case-distinct sibling as being inside the workspace.
    // Mirrors the PathComparison helper in the Azure DevOps extension, which is not linked into this shared
    // file. Runtime check on purpose: this ships as netstandard2.0, so a compile-time '#if' would not reflect
    // the platform the assembly actually runs on.
    private static readonly StringComparison PathComparison =
        System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows)
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    // Fully-qualified type prefixes for MSTest assertion implementations. A stack frame whose 'code' starts
    // with any of these is treated as framework internals and skipped when looking for the user's call site to
    // annotate. Matching on the type name (rather than the source file name) is robust to partial-class splits
    // (e.g. Assert.AreEqual.cs, Assert.IComparable.cs) and extension-based assertion implementations such as
    // Assert.That in Assert.That.cs, and it avoids false positives on user files innocently named *Assert.cs.
    // See https://github.com/microsoft/testfx/issues/6925.
    private static readonly string[] AssertionImplementationCodePrefixes =
    [
        "Microsoft.VisualStudio.TestTools.UnitTesting.Assert.",
        "Microsoft.VisualStudio.TestTools.UnitTesting.AssertExtensions.",
        "Microsoft.VisualStudio.TestTools.UnitTesting.CollectionAssert.",
        "Microsoft.VisualStudio.TestTools.UnitTesting.StringAssert.",
    ];

    /// <summary>
    /// Gets a value indicating whether MSTest assertion frames must be skipped manually on the current runtime.
    /// </summary>
    /// <remarks>
    /// MSTest's <c>Assert</c>, <c>CollectionAssert</c>, and <c>StringAssert</c> are all marked
    /// <c>[StackTraceHidden]</c>, which the CLR honors on .NET Core 2.1+ (i.e. every modern TFM) by omitting
    /// those frames from <see cref="System.Exception.StackTrace"/> altogether — so nothing needs skipping there.
    /// Only .NET Framework ignores <c>[StackTraceHidden]</c> and still surfaces the assertion frames, which is
    /// why the manual <see cref="AssertionImplementationCodePrefixes"/> skip only earns its keep there.
    /// <para>
    /// This is a <b>runtime</b> check on purpose: these extensions ship as <c>netstandard2.0</c> and that build
    /// is what loads under .NET Framework, so a compile-time <c>#if NETFRAMEWORK</c> would never be defined for
    /// the running assembly.
    /// </para>
    /// </remarks>
    public static bool SkipAssertionFramesForCurrentRuntime { get; } =
        System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription.StartsWith(".NET Framework", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Walks <paramref name="stackTrace"/> and returns the first frame that resolves to an existing file under
    /// <paramref name="repoRoot"/> (or a deterministic-build path), as a workspace-relative, forward-slash path
    /// plus its line number. Returns <see langword="null"/> when no such frame can be resolved.
    /// </summary>
    /// <param name="stackTrace">The exception stack trace string, or <see langword="null"/>.</param>
    /// <param name="repoRoot">The repository root used to relativize absolute paths (should end with a separator), or <see langword="null"/>.</param>
    /// <param name="fileSystem">File system used to verify the candidate file exists on disk.</param>
    /// <param name="logger">Logger for trace diagnostics.</param>
    /// <param name="skipAssertionFrames">
    /// When <see langword="true"/>, frames whose code matches a known MSTest assertion implementation are
    /// skipped. Production callers pass <see cref="SkipAssertionFramesForCurrentRuntime"/>.
    /// </param>
    /// <param name="shouldSkipFrame">Optional additional per-frame predicate (e.g. host-specific user filters).</param>
    public static (string RelativeNormalizedPath, int LineNumber)? TryResolve(
        string? stackTrace,
        string? repoRoot,
        IFileSystem fileSystem,
        ILogger logger,
        bool skipAssertionFrames,
        Func<string, bool>? shouldSkipFrame = null)
    {
        if (RoslynString.IsNullOrEmpty(stackTrace) || RoslynString.IsNullOrEmpty(repoRoot))
        {
            return null;
        }

        foreach (string stackFrame in stackTrace!.Split(NewlineCharacters, StringSplitOptions.RemoveEmptyEntries))
        {
            (string Code, string File, int LineNumber)? location = GetStackFrameLocation(stackFrame);
            if (location is null)
            {
                continue;
            }

            string file = location.Value.File;
            string code = location.Value.Code;

            if ((skipAssertionFrames && IsAssertionImplementationFrame(code))
                || (shouldSkipFrame is not null && shouldSkipFrame(code)))
            {
                if (logger.IsEnabled(LogLevel.Trace))
                {
                    logger.LogTrace($"Skipping stack frame '{code}' while resolving the source location.");
                }

                continue;
            }

            string? relativeNormalizedPath = TryMakeWorkspaceRelative(file, repoRoot, fileSystem);
            if (relativeNormalizedPath is null)
            {
                continue;
            }

            if (logger.IsEnabled(LogLevel.Trace))
            {
                logger.LogTrace($"Resolved source location '{relativeNormalizedPath}' (line {location.Value.LineNumber}).");
            }

            return (relativeNormalizedPath, location.Value.LineNumber);
        }

        return null;
    }

    /// <summary>
    /// Turns an absolute (or deterministic-build) source file path into a workspace-relative, forward-slash
    /// path suitable for a host annotation, or returns <see langword="null"/> when the file is outside
    /// <paramref name="repoRoot"/> or does not exist on disk.
    /// </summary>
    /// <remarks>
    /// Shared by the stack-trace walk above and by callers that resolve a location from test-node metadata
    /// (e.g. <c>TestFileLocationProperty</c>) rather than from an exception, so both paths relativize and
    /// validate identically.
    /// </remarks>
    /// <param name="filePath">The absolute or deterministic-build source file path, or <see langword="null"/>.</param>
    /// <param name="repoRoot">The repository root used to relativize absolute paths (should end with a separator), or <see langword="null"/>.</param>
    /// <param name="fileSystem">File system used to verify the candidate file exists on disk.</param>
    public static string? TryMakeWorkspaceRelative(string? filePath, string? repoRoot, IFileSystem fileSystem)
    {
        if (RoslynString.IsNullOrWhiteSpace(filePath) || RoslynString.IsNullOrEmpty(repoRoot))
        {
            return null;
        }

        if (!TryGetFullPath(repoRoot!, out string canonicalRepoRoot))
        {
            return null;
        }

        canonicalRepoRoot = EnsureTrailingDirectorySeparator(canonicalRepoRoot);

        string canonicalCandidate;
        if (filePath!.StartsWith(DeterministicBuildRoot, StringComparison.OrdinalIgnoreCase))
        {
            string relativePath = filePath.Substring(DeterministicBuildRoot.Length);
            if (!TryGetFullPath(canonicalRepoRoot, relativePath, out canonicalCandidate))
            {
                return null;
            }
        }
        else
        {
            if (!TryIsPathRooted(filePath, out bool isPathRooted))
            {
                return null;
            }

            // A test framework may report a path that is already workspace-relative: TestFileLocationProperty
            // does not require an absolute path, and the VSTest bridge copies TestCase.CodeFilePath verbatim.
            // Resolve such a path against the workspace root rather than dropping it; the containment and
            // existence checks below still reject traversal segments and files that are not on disk.
            bool resolved = isPathRooted
                ? TryGetFullPath(filePath, out canonicalCandidate)
                : TryGetFullPath(canonicalRepoRoot, filePath, out canonicalCandidate);

            if (!resolved)
            {
                return null;
            }
        }

        if (!IsUnderDirectory(canonicalCandidate, canonicalRepoRoot) || !fileSystem.ExistFile(canonicalCandidate))
        {
            return null;
        }

        // Annotations expect a workspace-relative path with forward slashes.
        return canonicalCandidate.Substring(canonicalRepoRoot.Length).Replace('\\', '/').TrimStart('/');
    }

    private static bool IsUnderDirectory(string path, string directory)
        => path.StartsWith(directory, PathComparison);

    private static string EnsureTrailingDirectorySeparator(string path)
    {
        char lastChar = path[path.Length - 1];
        return lastChar == Path.DirectorySeparatorChar || lastChar == Path.AltDirectorySeparatorChar
            ? path
            : path + Path.DirectorySeparatorChar;
    }

    private static bool TryIsPathRooted(string path, out bool isPathRooted)
    {
        try
        {
            isPathRooted = Path.IsPathRooted(path);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException
            or NotSupportedException
            or PathTooLongException
            or System.Security.SecurityException)
        {
            isPathRooted = false;
            return false;
        }
    }

    private static bool TryGetFullPath(string path, out string fullPath)
    {
        try
        {
            fullPath = Path.GetFullPath(path);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException
            or NotSupportedException
            or PathTooLongException
            or System.Security.SecurityException)
        {
            fullPath = string.Empty;
            return false;
        }
    }

    private static bool TryGetFullPath(string basePath, string relativePath, out string fullPath)
    {
        try
        {
            fullPath = Path.GetFullPath(Path.Combine(basePath, relativePath));
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException
            or NotSupportedException
            or PathTooLongException
            or System.Security.SecurityException)
        {
            fullPath = string.Empty;
            return false;
        }
    }

    private static bool IsAssertionImplementationFrame(string code)
    {
        foreach (string prefix in AssertionImplementationCodePrefixes)
        {
            if (code.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static (string Code, string File, int LineNumber)? GetStackFrameLocation(string stackTraceLine)
    {
        Match match = StackTraceHelper.GetFrameRegex().Match(stackTraceLine);
        if (!match.Success)
        {
            return null;
        }

        string code = match.Groups["code"].Value;
        if (RoslynString.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        string file = match.Groups["file"].Value;
        if (RoslynString.IsNullOrWhiteSpace(file))
        {
            return null;
        }

        int line = int.TryParse(match.Groups["line"].Value, out int value) ? value : 0;
        return (code, file, line);
    }
}
