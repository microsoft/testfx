// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Hosting;
using Microsoft.Testing.Platform.Builder;

using MelIConfiguration = Microsoft.Extensions.Configuration.IConfiguration;
using MelILoggerFactory = Microsoft.Extensions.Logging.ILoggerFactory;

namespace Microsoft.Testing.Extensions;

/// <summary>
/// Extension methods for composing an application-owned <see cref="IHost"/> with Microsoft Testing Platform.
/// </summary>
/// <remarks>
/// This API is experimental. It may change, break, or be removed at any time without notice.
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public static class MicrosoftExtensionsHostingExtensions
{
    /// <summary>
    /// Starts the host, runs a Microsoft Testing Platform application, and stops the host.
    /// </summary>
    /// <param name="host">The application-owned host.</param>
    /// <param name="args">The test application command-line arguments.</param>
    /// <param name="configure">Registers the test framework and optional MTP extensions.</param>
    /// <param name="cancellationToken">A token used while starting the host.</param>
    /// <returns>The Microsoft Testing Platform exit code.</returns>
    /// <remarks>
    /// <para>
    /// The host remains owned by the caller and is not disposed by this method. Its configuration is
    /// imported as a read-only snapshot, and its logger factory is borrowed without transferring ownership.
    /// </para>
    /// <para>
    /// The host is started before the MTP application is built so hosted observability providers can
    /// subscribe before MTP creates diagnostic activities and meters. The host is stopped in a
    /// <see langword="finally"/> block after MTP completes or fails.
    /// </para>
    /// <para>
    /// Composition is process-local. Host services are not propagated to separately launched test host
    /// or controller processes.
    /// </para>
    /// </remarks>
    public static async Task<int> RunTestingPlatformAsync(
        this IHost host,
        string[] args,
        Action<ITestApplicationBuilder> configure,
        CancellationToken cancellationToken = default)
    {
        _ = host ?? throw new ArgumentNullException(nameof(host));
        _ = args ?? throw new ArgumentNullException(nameof(args));
        _ = configure ?? throw new ArgumentNullException(nameof(configure));

        MelIConfiguration configuration = GetRequiredHostService<MelIConfiguration>(host.Services);
        MelILoggerFactory loggerFactory = GetRequiredHostService<MelILoggerFactory>(host.Services);

        ITestApplicationBuilder testApplicationBuilder = await TestApplication.CreateBuilderAsync(args).ConfigureAwait(false);
        testApplicationBuilder.AddMicrosoftExtensionsConfigurationSnapshot(configuration);
        testApplicationBuilder.AddMicrosoftExtensionsLogging(loggerFactory);
        configure(testApplicationBuilder);

        await host.StartAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using ITestApplication testApplication = await testApplicationBuilder.BuildAsync().ConfigureAwait(false);
            return await testApplication.RunAsync().ConfigureAwait(false);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    private static TService GetRequiredHostService<TService>(IServiceProvider services)
        where TService : class
        => services.GetService(typeof(TService)) as TService
            ?? throw new InvalidOperationException(
                $"The application host does not provide the required service '{typeof(TService).FullName}'.");
}
