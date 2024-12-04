// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.Helpers;

[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
[SuppressMessage("ApiDesign", "RS0016:Add public types and members to the declared API", Justification = "Experimental API")]
public static class TestApplicationBuilderExtensions
{
    public static void AddTreeNodeFilterService(this ITestApplicationBuilder testApplicationBuilder, IExtension extension)
    {
        testApplicationBuilder.CommandLine.AddProvider(() => new TreeNodeFilterCommandLineOptionsProvider(extension));
        testApplicationBuilder.TestHost.RegisterTestExecutionFilter(sp => new TreeNodeFilter(sp.GetCommandLineOptions()));
    }
}
