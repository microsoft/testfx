// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;

namespace Microsoft.Testing.Platform.Helpers;

internal static class TestApplicationBuilderExtensions
{
    public static void AddTreeNodeFilterService(this TestApplicationBuilder testApplicationBuilder, IExtension extension)
        => testApplicationBuilder.CommandLine.AddProvider(() => new TreeNodeFilterCommandLineOptionsProvider(extension));
}
