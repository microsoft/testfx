// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;

namespace Microsoft.Testing.Platform;

internal static class DotnetTestHelper
{
    public static bool HasDotnetTestServerOption(this CommandLineHandler commandLineHandler) =>
        commandLineHandler.TryGetOptionArgument(PlatformCommandLineProvider.ServerOptionKey, out string? serverArg) &&
        serverArg.Equals(PlatformCommandLineProvider.DotnetTestCliProtocolName, StringComparison.Ordinal);
}
