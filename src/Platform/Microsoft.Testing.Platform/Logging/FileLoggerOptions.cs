// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Logging;

internal sealed class FileLoggerOptions(
    string logFolder,
    string logPrefixName,
    string? fileName = null,
    bool syncFlush = true)
{
    public string LogFolder { get; private set; } = logFolder;

    public string LogPrefixName { get; private set; } = logPrefixName;

    public string? FileName { get; private set; } = fileName;

    public bool SyncFlush { get; private set; } = syncFlush;
}
