// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.CommandLine;

internal sealed class TreeNodeFilterCommandLineOptionsProvider(IExtension extension) : CommandLineOptionsProviderBase(extension, [new(TreenodeFilter, PlatformResources.TreeNodeFilterDescription, ArgumentArity.ExactlyOne, false)])
{
    public const string TreenodeFilter = "treenode-filter";
}
