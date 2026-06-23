// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.CtrfReport.Resources;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.CtrfReport;

internal sealed partial class CtrfReportEngine
{
    private async Task<(string FileName, string? Warning)> WriteWithRetryAsync(string finalPath, byte[] bytes, bool fileNameExplicitlyProvided)
    {
        // Explicit file names: use FileMode.Create (overwrite). Default-generated file
        // names: use FileMode.CreateNew but retry with disambiguating suffixes when the
        // file already exists, so concurrent runs (or two runs within the same second
        // sharing the result directory) don't fail with IOException.
        if (fileNameExplicitlyProvided)
        {
            bool willOverwrite = _fileSystem.ExistFile(finalPath);
            await WriteAsync(finalPath, FileMode.Create, bytes).ConfigureAwait(false);
            return (
                finalPath,
                willOverwrite
                    ? string.Format(CultureInfo.InvariantCulture, ExtensionResources.CtrfReportFileExistsAndWillBeOverwritten, finalPath)
                    : null);
        }

        DateTimeOffset firstTry = _clock.UtcNow;
        string directory = Path.GetDirectoryName(finalPath) ?? string.Empty;
        string fileName = Path.GetFileName(finalPath);
        SplitCtrfExtension(fileName, out string baseName, out string extension);
        string candidate = finalPath;
        int attempt = 0;

        while (true)
        {
            _cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await WriteAsync(candidate, FileMode.CreateNew, bytes).ConfigureAwait(false);
                return (candidate, null);
            }
            catch (IOException) when (_fileSystem.ExistFile(candidate))
            {
                // The IOException was caused by the file already existing. Try a
                // suffixed name. Any other IOException (disk full, permission, path
                // too long, etc.) is not caught here and will propagate to the caller.
                // Bound by both wall-clock (5s) and attempt count (1000) so we never
                // spin forever in pathological cases like a clock that doesn't advance.
                if (_clock.UtcNow - firstTry > TimeSpan.FromSeconds(5) || attempt >= 1_000)
                {
                    throw;
                }

                attempt++;
                candidate = Path.Combine(directory, $"{baseName}_{attempt}{extension}");
            }
        }
    }

    // Split a file name into base + extension while preserving the CTRF
    // double-extension convention (`*.ctrf.json`). The disambiguation suffix
    // must land before `.ctrf.json` so that downstream CTRF readers continue to
    // recognize the file by its conventional extension.
    private static void SplitCtrfExtension(string fileName, out string baseName, out string extension)
    {
        const string ctrfJsonSuffix = ".ctrf.json";
        if (fileName.EndsWith(ctrfJsonSuffix, StringComparison.OrdinalIgnoreCase) && fileName.Length > ctrfJsonSuffix.Length)
        {
            baseName = fileName.Substring(0, fileName.Length - ctrfJsonSuffix.Length);
            extension = fileName.Substring(fileName.Length - ctrfJsonSuffix.Length);
            return;
        }

        baseName = Path.GetFileNameWithoutExtension(fileName);
        extension = Path.GetExtension(fileName);
    }

    private async Task WriteAsync(string path, FileMode mode, byte[] bytes)
    {
        // Note that we need to dispose the IFileStream, not the inner stream.
        // IFileStream implementations will be responsible to dispose their inner stream.
        using IFileStream stream = _fileSystem.NewFileStream(path, mode);
#if NETCOREAPP
        await stream.Stream.WriteAsync(bytes.AsMemory(), _cancellationToken).ConfigureAwait(false);
#else
        await stream.Stream.WriteAsync(bytes, 0, bytes.Length, _cancellationToken).ConfigureAwait(false);
#endif
    }
}
