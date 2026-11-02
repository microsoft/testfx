// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.Diagnostics;

internal static class HangDumpOptions
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(30);

    public static bool IsEnabled(ICommandLineOptions commandLineOptions)
        => commandLineOptions.IsOptionSet(HangDumpCommandLineProvider.HangDumpOptionName);

    public static TimeSpan GetTimeout(ICommandLineOptions commandLineOptions)
        => commandLineOptions.TryGetOptionArgumentList(HangDumpCommandLineProvider.HangDumpTimeoutOptionName, out string[]? timeout)
            ? TimeSpanParser.Parse(timeout[0])
            : DefaultTimeout;
}
