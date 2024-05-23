// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using Microsoft.Testing.Platform.CommandLine;

namespace Microsoft.Testing.Platform.TestHostControllers;

internal sealed class TestHostControllerInfo(CommandLineParseResult parseResult) : ITestHostControllerInfo
{
    public bool HasTestHostController => parseResult.IsOptionSet(PlatformCommandLineProvider.TestHostControllerPIDOptionKey);

    public bool? CurrentProcessIsTestHostController { get; set; }

    public int? GetTestHostControllerPID(bool throwIfMissing = true) => parseResult.TryGetOptionArgumentList(PlatformCommandLineProvider.TestHostControllerPIDOptionKey, out string[]? pid)
                ? int.Parse(pid[0], CultureInfo.InvariantCulture)
                : throwIfMissing ? throw new InvalidOperationException($"'{PlatformCommandLineProvider.TestHostControllerPIDOptionKey}' not found in the command line") : null;
}
