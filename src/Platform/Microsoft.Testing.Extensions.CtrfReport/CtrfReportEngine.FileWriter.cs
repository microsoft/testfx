// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.CtrfReport.Resources;

namespace Microsoft.Testing.Extensions.CtrfReport;

internal sealed partial class CtrfReportEngine
{
    private async Task<(string FileName, string? Warning)> WriteAsync(string finalPath, byte[] bytes)
    {
        // Always overwrite (FileMode.Create), regardless of whether the file name was explicitly
        // provided or generated from the default
        // <user>_<machine>_<asm>_<tfm>_<timestamp>.ctrf.json shape. Emit a warning when overwriting
        // so users have a single, predictable rule to reason about — matching the TRX, HTML and
        // JUnit report extensions.
        bool willOverwrite = _fileSystem.ExistFile(finalPath);
        await WriteBytesAsync(finalPath, FileMode.Create, bytes).ConfigureAwait(false);
        return (
            finalPath,
            willOverwrite
                ? string.Format(CultureInfo.InvariantCulture, ExtensionResources.CtrfReportFileExistsAndWillBeOverwritten, finalPath)
                : null);
    }
}
