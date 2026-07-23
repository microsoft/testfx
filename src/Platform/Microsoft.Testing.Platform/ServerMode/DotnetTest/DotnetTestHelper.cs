// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;

namespace Microsoft.Testing.Platform;

internal enum DotnetTestTransportKind
{
    NamedPipe,
    Http,
}

internal static class DotnetTestHelper
{
    public static bool HasDotnetTestServerOption(this CommandLineHandler commandLineHandler) =>
        commandLineHandler.TryGetOptionArgumentList(PlatformCommandLineProvider.ServerOptionKey, out string[]? serverArgs) &&
        serverArgs.Length == 1 &&
        serverArgs[0].Equals(PlatformCommandLineProvider.DotnetTestCliProtocolName, StringComparison.OrdinalIgnoreCase);

    public static bool TryGetDotnetTestTransport(this CommandLineHandler commandLineHandler, out DotnetTestTransportKind transport)
    {
        if (commandLineHandler.TryGetOptionArgumentList(PlatformCommandLineProvider.DotNetTestTransportOptionKey, out string[]? transportArgs)
            && transportArgs is { Length: 1 }
            && PlatformCommandLineProvider.DotNetTestTransportHttpArgument.Equals(transportArgs[0], StringComparison.OrdinalIgnoreCase))
        {
            transport = DotnetTestTransportKind.Http;
            return true;
        }

        if (commandLineHandler.IsOptionSet(PlatformCommandLineProvider.DotNetTestPipeOptionKey))
        {
            transport = DotnetTestTransportKind.NamedPipe;
            return true;
        }

        transport = default;
        return false;
    }
}
