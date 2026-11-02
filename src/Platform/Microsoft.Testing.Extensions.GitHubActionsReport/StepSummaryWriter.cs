// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Extensions.GitHubActionsReport;

/// <summary>
/// Every write this extension makes to the shared <c>GITHUB_STEP_SUMMARY</c> file.
/// </summary>
/// <remarks>
/// The file is shared: one test-host process per assembly and target framework appends to it, the aggregated
/// <c>dotnet test</c> post-processor rewrites its own section in it, and other steps and test frameworks append
/// their own content. Every access therefore needs the same file system, path, retry policy and lock, which is
/// what this type holds so its methods can take only what actually varies between calls.
/// </remarks>
internal sealed partial class StepSummaryWriter
{
    /// <summary>
    /// Ceiling on how much of the shared summary file this writer will read into memory in one go.
    /// </summary>
    /// <remarks>
    /// Callers that pass no explicit bound would otherwise size a buffer from a file other producers control.
    /// Sixty-four megabytes is far above any summary GitHub would accept — it discards anything over 1 MB — and
    /// far below the point where the allocation itself is the failure.
    /// </remarks>
    private const long MaxReadableSummaryBytes = 64L * 1024 * 1024;

    private readonly IFileSystem _fileSystem;
    private readonly ILogger _logger;
    private readonly int _maxAttempts;
    private readonly TimeSpan _retryDelay;

    internal StepSummaryWriter(IFileSystem fileSystem, string path, ILogger logger, int maxAttempts, TimeSpan retryDelay)
    {
        _fileSystem = fileSystem;
        Path = path;
        _logger = logger;
        _maxAttempts = maxAttempts;
        _retryDelay = retryDelay;
    }

    /// <summary>
    /// Gets the path of the shared summary file, for diagnostics that name it.
    /// </summary>
    internal string Path { get; }

    /// <summary>
    /// The lock every writer to the shared summary file takes for the duration of its update.
    /// </summary>
    /// <remarks>
    /// Opening the summary itself exclusively is enough to serialize plain appends, but not an update that has to
    /// replace the file: a file cannot be replaced while it is open, so the handle must be released before the
    /// swap, and a sibling appending in that gap would have its section overwritten by content captured before it.
    /// A separate lock file closes that window because it is held across the whole read-modify-replace, and it is
    /// the same lock the aggregated path uses, so the two writing modes serialize against each other too.
    /// </remarks>
    private string GetSummaryLockPath()
        => Path + ".microsoft-testing-platform.lock";

    /// <summary>
    /// Acquires <see cref="GetSummaryLockPath"/>, retrying while another writer holds it.
    /// </summary>
    private async Task<IFileStream> AcquireSummaryLockAsync(
        CancellationToken cancellationToken)
    {
        string lockPath = GetSummaryLockPath();
        for (int attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return _fileSystem.NewFileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (attempt < _maxAttempts)
            {
                await Task.Delay(_retryDelay, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
