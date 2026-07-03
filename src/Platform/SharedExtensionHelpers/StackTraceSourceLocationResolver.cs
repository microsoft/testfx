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

            string relativePath;
            if (file.StartsWith(DeterministicBuildRoot, StringComparison.OrdinalIgnoreCase))
            {
                relativePath = file.Substring(DeterministicBuildRoot.Length);
            }
            else if (file.StartsWith(repoRoot!, StringComparison.OrdinalIgnoreCase))
            {
                relativePath = file.Substring(repoRoot!.Length);
            }
            else
            {
                continue;
            }

            string fullPath = Path.Combine(repoRoot!, relativePath);
            if (!fileSystem.ExistFile(fullPath))
            {
                continue;
            }

            // Annotations expect a workspace-relative path with forward slashes.
            string relativeNormalizedPath = relativePath.Replace('\\', '/').TrimStart('/');
            if (logger.IsEnabled(LogLevel.Trace))
            {
                logger.LogTrace($"Resolved source location '{relativeNormalizedPath}' (line {location.Value.LineNumber}).");
            }

            return (relativeNormalizedPath, location.Value.LineNumber);
        }

        return null;
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
