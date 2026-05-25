// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace Microsoft.Testing.Extensions.MSBuild;

internal sealed class MSBuildCommandLineProvider : CommandLineOptionsProviderBase
{
    private static readonly IReadOnlyCollection<CommandLineOption> CachedCommandLineOptions =
    [
        new(MSBuildConstants.MSBuildNodeOptionKey, "Used to pass the MSBuild node handle", ArgumentArity.ExactlyOne, isHidden: true, isBuiltIn: true)
    ];

    public MSBuildCommandLineProvider()
        : base(
            nameof(MSBuildCommandLineProvider),
            ExtensionVersion.DefaultSemVer,
            nameof(MSBuildCommandLineProvider),
            Resources.ExtensionResources.MSBuildExtensionsDescription,
            CachedCommandLineOptions)
    {
    }
}
