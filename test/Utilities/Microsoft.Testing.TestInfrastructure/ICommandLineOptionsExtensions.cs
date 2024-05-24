// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;

namespace Microsoft.Testing.TestInfrastructure;

public static class ICommandLineOptionsExtensions
{
    public static bool IsServerMode(this ICommandLineOptions commandLineOptions) => commandLineOptions.IsOptionSet("--server");
}
