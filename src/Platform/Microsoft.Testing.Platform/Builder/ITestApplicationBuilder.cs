// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.TestFramework;
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
    /// Registers a test framework with the application builder.
    /// </summary>
    /// <param name="capabilitiesFactory">The factory method for creating test framework capabilities.</param>
    /// <param name="adapterFactory">The factory method for creating a test framework adapter.</param>
    /// <returns>The updated test application builder.</returns>
    ITestApplicationBuilder RegisterTestFramework(
        Func<IServiceProvider, ITestFrameworkCapabilities> capabilitiesFactory,
        Func<ITestFrameworkCapabilities, IServiceProvider, ITestFramework> adapterFactory);

    /// <summary>
    /// Builds the test application asynchronously.
    /// </summary>
    /// <returns>A task representing the asynchronous operation. The task result is the built test application.</returns>
    Task<ITestApplication> BuildAsync();
}
