// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.PackagedApp;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;

namespace Microsoft.Testing.Extensions;

/// <summary>
/// Provides extension methods for adding Windows test host launch support for packaged MSIX apps
/// (desktop and windowsApp/UWP), and — when opted in — non-packaged loose layouts.
/// </summary>
public static class PackagedAppExtensions
{
    /// <summary>
    /// Restores the platform-prepared argument array delivered to a windowsApp/UWP test host through
    /// <c>LaunchActivatedEventArgs.Arguments</c>.
    /// </summary>
    /// <remarks>
    /// Call this method from the application's <c>OnLaunched</c> override before
    /// <see cref="TestApplication.CreateBuilderAsync(string[], TestApplicationOptions?)"/>. It also
    /// restores the transient controller connect-back environment that AUMID activation does not
    /// inherit. Packaged full-trust desktop apps should continue to use their normal process arguments.
    /// </remarks>
    /// <param name="activationArguments">The opaque activation string supplied by Windows.</param>
    /// <returns>The exact logical argument array prepared by Microsoft Testing Platform.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="activationArguments"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">The activation string is not a valid Microsoft Testing Platform payload.</exception>
    public static string[] GetTestApplicationArguments(string activationArguments)
        => PackagedAppConnectBackReader.ReadActivationArgumentsAndApplyConnectBack(activationArguments);

    /// <summary>
    /// Registers a test host launcher for Windows test applications. Packaged MSIX hosts are registered
    /// with the OS and activated by Application User Model ID in the Windows build of this extension
    /// (see https://github.com/microsoft/testfx/issues/9933); the plain build rejects a packaged layout
    /// with an actionable error. Full-trust desktop hosts receive normal process arguments, while
    /// windowsApp/UWP hosts restore launch activation arguments with
    /// <see cref="GetTestApplicationArguments(string)"/>. The launcher also contributes the selected
    /// application's exact package SID to the controller connection through
    /// <see cref="ITestHostControllerConnectionAuthorizer"/>. These are communication primitives; the
    /// SDK/platform startup path still routes true UWP/AppContainer test projects to VSTest rather than
    /// starting them as MTP test hosts.
    /// </summary>
    /// <remarks>
    /// The launcher only enables itself when the test application is a packaged layout, so calling this
    /// from an app that is started normally — an unpackaged WinUI app, or an ordinary console test app —
    /// leaves the platform on its default launch path and costs nothing. Set
    /// <c>TESTINGPLATFORM_PACKAGEDAPP_LAUNCHER</c> to <c>always</c> to opt a non-packaged (loose) layout
    /// into deploy-and-launch, or to <c>never</c> to disable the launcher entirely.
    /// </remarks>
    /// <param name="builder">The test application builder.</param>
    public static void AddPackagedAppDeployment(this ITestApplicationBuilder builder)
    {
        _ = builder ?? throw new System.ArgumentNullException(nameof(builder));

        // When this process is a packaged (MSIX) test host activated by AUMID it did not inherit the
        // controller-to-host connect-back environment variables; restore them from the launcher's
        // hand-off before the platform reads that environment when it builds the test application. This
        // runs for both the MSBuild-registered hook and direct callers of this method. It is a no-op for
        // the controller, for non-packaged layouts, and when there is no handshake to consume. Note that
        // environment consumed strictly earlier during CreateBuilderAsync (culture, config discovery) is
        // not reproduced for the packaged path; only the platform connect-back variables are.
        PackagedAppConnectBackReader.TryApplyConnectBackEnvironment(Environment.GetCommandLineArgs());

        builder.TestHostControllers.AddTestHostLauncher(_ => new PackagedAppTestHostLauncher());
    }
}
