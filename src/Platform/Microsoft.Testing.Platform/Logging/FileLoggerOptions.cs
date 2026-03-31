// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.Logging;

internal sealed class FileLoggerOptions
{
    public string LogFolder { get; }

    public string LogPrefixName { get; }

    public string? FileName { get; }

    public bool SyncFlush { get; }

    public FileLoggerOptions(
        string logFolder,
        string logPrefixName,
        string? fileName = null,
        bool syncFlush = true)
    {
        LogFolder = logFolder;
        LogPrefixName = logPrefixName;
        FileName = fileName;
        SyncFlush = syncFlush;

        if (syncFlush && OperatingSystem.IsBrowser())
        {
            throw new PlatformNotSupportedException(PlatformResources.SyncFlushNotSupportedInBrowserErrorMessage);
        }
    }
}
