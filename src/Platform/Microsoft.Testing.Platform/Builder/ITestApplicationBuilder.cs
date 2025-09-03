// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Telemetry;
using Microsoft.Testing.Platform.TestHost;
using Microsoft.Testing.Platform.TestHostControllers;

namespace Microsoft.Testing.Platform.Builder;

/// <summary>
/// Represents an interface for building test applications.
/// </summary>
public interface ITestApplicationBuilder
{
    /// <summary>
    /// Gets the test host manager.
    /// </summary>
    ITestHostManager TestHost { get; }

    /// <summary>
    /// Gets the test host controllers manager.
    /// </summary>
    ITestHostControllersManager TestHostControllers { get; }

    /// <summary>
    /// Gets the command line manager.
    /// </summary>
    ICommandLineManager CommandLine { get; }

    /// <summary>
    /// Gets the configuration manager.
    /// </summary>
    [Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
    IConfigurationManager Configuration { get; }

    /// <summary>
    /// Gets the logging manager.
    /// </summary>
    [Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
    ILoggingManager Logging { get; }

    /// <summary>
    /// Gets the telemetry manager used to collect and report diagnostic data for the testing platform.
    /// </summary>
    /// <remarks>Telemetry functionality is experimental and may change in future releases. Use this property
    /// to access diagnostic metrics and events related to test execution and platform health.</remarks>
    [Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
    ITelemetryManager Telemetry { get; }

    /// <summary>
    /// Registers a test framework with the application builder.
    /// </summary>
    /// <param name="capabilitiesFactory">The factory method for creating test framework capabilities.</param>
    /// <param name="frameworkFactory">The factory method for creating a test framework adapter.</param>
    /// <returns>The updated test application builder.</returns>
    ITestApplicationBuilder RegisterTestFramework(
        Func<IServiceProvider, ITestFrameworkCapabilities> capabilitiesFactory,
        Func<ITestFrameworkCapabilities, IServiceProvider, ITestFramework> frameworkFactory);

    /// <summary>
    /// Builds the test application asynchronously.
    /// </summary>
    /// <returns>A task representing the asynchronous operation. The task result is the built test application.</returns>
    Task<ITestApplication> BuildAsync();
}
