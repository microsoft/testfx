// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using SL = Microsoft.Build.Logging.StructuredLogger;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

/// <summary>
/// Reads the MSBuild binary logs that the acceptance tests assert over. Always read binlogs through this helper,
/// never through <c>Serialization.Read</c> directly.
/// </summary>
/// <remarks>
/// <para>
/// <c>Microsoft.Build.Logging.StructuredLogger.Serialization.Read</c> is not safe to call concurrently in a cold
/// process. The first concurrent reads race on the library's lazy static initialization, and a read that loses the
/// race does not throw: it returns a <c>Build</c> whose only children are an <c>[Error]</c> reading
/// "Error when opening the log file." and a warning, with no build content underneath. Every assertion made over
/// that tree is wrong. Positive assertions fail for no product reason, and negative assertions such as
/// <c>Assert.DoesNotContain</c> pass vacuously, so the race erodes coverage as well as reddening builds.
/// </para>
/// <para>
/// Both acceptance suites parallelize at method level, so binlog reads do overlap. Taking a single process-wide
/// lock around the read removes the race. Reading twelve binlogs costs about 1.00s serialized against 0.74s
/// unserialized, which is nothing next to a suite that takes twenty minutes.
/// </para>
/// </remarks>
internal static class BinlogReader
{
    /// <summary>
    /// The text the reader puts on the error node it substitutes for the tree when it cannot open a binlog.
    /// </summary>
    private const string OpenFailureErrorText = "Error when opening the log file.";

    private static readonly Lock ReadLock = new();

    /// <summary>
    /// Reads <paramref name="binlogPath"/>, serialized against every other read that goes through this helper.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The reader returned an unusable tree. Never returns a silently empty <c>Build</c>.
    /// </exception>
    public static SL.Build Read(string binlogPath)
    {
        SL.Build build;
        lock (ReadLock)
        {
            build = SL.Serialization.Read(binlogPath);
        }

        // The lock is what removes the race, so this check is not expected to fire. It is here so that a future
        // version of the reader cannot quietly reintroduce an empty tree and turn every assertion over it into a
        // meaningless result. There is no retry: a read that fails with the lock held fails for a reason reading
        // the same file again will not change.
        string? corruption = DescribeCorruption(build);

        return corruption is null
            ? build
            : throw new InvalidOperationException(
                $"Could not read the binlog '{binlogPath}' ({DescribeFile(binlogPath)}). " +
                $"{corruption} Asserting over the returned tree would be meaningless, so the read fails here instead.");
    }

    private static string? DescribeCorruption(SL.Build build)
    {
        // Deliberately not keyed on build.Succeeded: several call sites read the binlog of a build that failed on
        // purpose. These are the shapes that only a failed read produces.
        SL.Error? openFailure = build.FindFirstChild<SL.Error>(error => error.Text == OpenFailureErrorText);

        return openFailure is not null
            ? $"The reader returned an empty tree carrying '{openFailure.Text}'."
            : build.FindFirstDescendant<SL.AddItem>() is null
                ? "The tree holds no AddItem nodes at all, which no real build produces."
                : null;
    }

    private static string DescribeFile(string binlogPath)
    {
        try
        {
            FileInfo file = new(binlogPath);
            return file.Exists ? $"{file.Length} bytes on disk" : "no such file on disk";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return $"size unavailable: {ex.Message}";
        }
    }
}
