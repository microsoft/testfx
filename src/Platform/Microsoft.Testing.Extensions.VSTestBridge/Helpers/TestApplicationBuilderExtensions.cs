// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.VSTestBridge.CommandLine;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Extensions;

namespace Microsoft.Testing.Extensions.VSTestBridge.Helpers;

/// <summary>
/// A set of helper methods to register the VSTest services into Microsoft Testing Platform.
/// </summary>
public static class TestApplicationBuilderExtensions
{
    /// <summary>
    /// Allows to register the VSTest filter service.
    /// </summary>
    /// <param name="builder">The test application builder.</param>
    /// <param name="extension">The extension that will be used as the source of registration for this helper service.</param>
    public static void AddTestCaseFilterService(this ITestApplicationBuilder builder, IExtension extension)
        => builder.CommandLine.AddProvider(() => new TestCaseFilterCommandLineOptionsProvider(extension));

    /// <summary>
    /// Allows to register the VSTest runsettings service.
    /// </summary>
    /// <param name="builder">The test application builder.</param>
    /// <param name="extension">The extension that will be used as the source of registration for this helper service.</param>
    public static void AddRunSettingsService(this ITestApplicationBuilder builder, IExtension extension)
        => builder.CommandLine.AddProvider(() => new RunSettingsCommandLineOptionsProvider(extension));

    /// <summary>
    /// Allows to register the VSTest TestRunParameters service.
    /// </summary>
    /// <param name="builder">The test application builder.</param>
    /// <param name="extension">The extension that will be used as the source of registration for this helper service.</param>
    public static void AddTestRunParametersService(this ITestApplicationBuilder builder, IExtension extension)
        => builder.CommandLine.AddProvider(() => new TestRunParametersCommandLineOptionsProvider(extension));
}
