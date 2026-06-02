// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;

namespace Microsoft.Testing.Extensions.Diagnostics;

internal static class HangDumpOptions
{
    public static bool IsEnabled(ICommandLineOptions commandLineOptions)
        => commandLineOptions.IsOptionSet(HangDumpCommandLineProvider.HangDumpOptionName);
}
