// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.VideoRecorder;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions;

/// <summary>
/// Provides extension methods to add the video recorder to a test application.
/// </summary>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public static class VideoRecorderExtensions
{
    /// <summary>
    /// Adds the video recorder to the test application. When the run is started with
    /// <c>--capture-video</c>, the screen is recorded automatically (one video per test by
    /// default, or one chaptered video per session).
    /// </summary>
    /// <param name="builder">The test application builder.</param>
    /// <param name="configure">An optional callback to configure recording options.</param>
    [UnsupportedOSPlatform("browser")]
    [UnsupportedOSPlatform("ios")]
    [UnsupportedOSPlatform("tvos")]
    [UnsupportedOSPlatform("wasi")]
    public static void AddVideoRecorderProvider(this ITestApplicationBuilder builder, Action<VideoRecorderOptions>? configure = null)
    {
        // Build a fresh options instance per session inside the factory. The session handler mutates
        // its options via command-line overrides, so a single shared instance would leak the first
        // session's overrides into later sessions when one process hosts several (e.g. hot reload or
        // repeated runs).
        var compositeFactory = new CompositeExtensionFactory<VideoRecorderSessionHandler>(serviceProvider =>
        {
            var options = new VideoRecorderOptions();
            configure?.Invoke(options);

            return new VideoRecorderSessionHandler(
                options,
                serviceProvider.GetConfiguration(),
                serviceProvider.GetCommandLineOptions(),
                serviceProvider.GetMessageBus(),
                serviceProvider.GetOutputDevice(),
                serviceProvider.GetSystemClock(),
                serviceProvider.GetLoggerFactory().CreateLogger<VideoRecorderSessionHandler>());
        });

        builder.TestHost.AddDataConsumer(compositeFactory);
        builder.TestHost.AddTestSessionLifetimeHandler(compositeFactory);
        builder.CommandLine.AddProvider(() => new VideoRecorderCommandLineProvider());
    }
}
