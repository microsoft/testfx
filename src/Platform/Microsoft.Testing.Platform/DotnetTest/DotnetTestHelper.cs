// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;

namespace Microsoft.Testing.Platform.DotnetTest;

internal static class DotnetTestHelper
{
    private const string DotnetTestCliProtocol = "dotnettestcli";

    public static bool HasDotnetTestServerOption(CommandLineHandler commandLineHandler) =>
        commandLineHandler.TryGetOptionArgumentList(PlatformCommandLineProvider.ServerOptionKey, out string[]? serverArgs) &&
        serverArgs.Length == 1 &&
        serverArgs[0].Equals(DotnetTestCliProtocol, StringComparison.Ordinal);
}
