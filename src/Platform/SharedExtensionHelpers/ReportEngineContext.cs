// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions;

/// <summary>
/// Bundles the full set of platform services and call-time arguments that every report engine
/// (<c>HtmlReportEngine</c>, <c>JUnitReportEngine</c>, <c>CtrfReportEngine</c>, ...) needs. Centralizing
/// this list means a new <see cref="ReportEngineBase"/> dependency only has to be threaded through one
/// place instead of being copied into every <c>ReportGeneratorBase</c>-derived generator.
/// </summary>
internal sealed record ReportEngineContext(
    IFileSystem FileSystem,
    ITestApplicationModuleInfo TestApplicationModuleInfo,
    IEnvironment Environment,
    ICommandLineOptions CommandLineOptions,
    IConfiguration Configuration,
    IClock Clock,
    ITestFramework TestFramework,
    DateTimeOffset TestStartTime,
    int ExitCode,
    CancellationToken CancellationToken);
