// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.IPC;

namespace Microsoft.Testing.Platform;

internal static class DotnetTestHelper
{
    public static bool HasDotnetTestServerOption(this CommandLineHandler commandLineHandler) =>
        commandLineHandler.TryGetOptionArgumentList(PlatformCommandLineProvider.ServerOptionKey, out string[]? serverArgs) &&
        serverArgs.Length == 1 &&
        serverArgs[0].Equals(Constants.DotnetTestCliProtocol, StringComparison.Ordinal);
}
