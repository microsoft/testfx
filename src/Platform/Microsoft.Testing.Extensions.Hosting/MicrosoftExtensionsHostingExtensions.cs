// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Helpers;

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
    /// Determines whether a generated entry point should run an informational MTP command without creating the application host.
    /// </summary>
    /// <param name="args">The MTP command-line arguments.</param>
    /// <returns><see langword="true"/> for help and info options, including options expanded from response files.</returns>
    /// <remarks>
    /// This API is experimental. It may change, break, or be removed at any time without notice.
    /// </remarks>
    public static bool ShouldBypassApplicationHost(string[] args)
    {
        _ = args ?? throw new ArgumentNullException(nameof(args));

        CommandLineParseResult parseResult = CommandLineParser.Parse(args, new SystemEnvironment());
        return parseResult.Options.Any(
            static option => option.Name.Equals(PlatformCommandLineProvider.HelpOptionKey, StringComparison.OrdinalIgnoreCase)
                || option.Name.Equals(PlatformCommandLineProvider.HelpOptionQuestionMark, StringComparison.OrdinalIgnoreCase)
                || option.Name.Equals(PlatformCommandLineProvider.InfoOptionKey, StringComparison.OrdinalIgnoreCase));
    }

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

        MelIConfiguration configuration = host.Services.GetRequiredService<MelIConfiguration>();
        MelILoggerFactory loggerFactory = host.Services.GetRequiredService<MelILoggerFactory>();
        IHostApplicationLifetime? hostApplicationLifetime = host.Services.GetService<IHostApplicationLifetime>();
        HostApplicationLifetimeBridge? hostApplicationLifetimeBridge = hostApplicationLifetime is null
            ? null
            : new(hostApplicationLifetime);
        var testApplicationOptions = new TestApplicationOptions
        {
            HostLifetimeBridge = hostApplicationLifetimeBridge,
        };

        ITestApplicationBuilder testApplicationBuilder = await TestApplication.CreateBuilderAsync(args, testApplicationOptions).ConfigureAwait(false);
        try
        {
            testApplicationBuilder.AddMicrosoftExtensionsConfigurationSnapshot(configuration);
            testApplicationBuilder.AddMicrosoftExtensionsLogging(loggerFactory);
            configure(testApplicationBuilder);

            Exception? operationException = null;
            try
            {
                await host.StartAsync(cancellationToken).ConfigureAwait(false);
                using ITestApplication testApplication = await testApplicationBuilder.BuildAsync().ConfigureAwait(false);
                return await testApplication.RunAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                operationException = exception;
                throw;
            }
            finally
            {
                hostApplicationLifetimeBridge?.Disconnect();
                try
                {
                    await host.StopAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception stopException) when (operationException is not null)
                {
                    AddSecondaryExceptionOrThrow(operationException, nameof(IHost.StopAsync), stopException);
                }
            }
        }
        catch (Exception exception)
        {
            try
            {
                if (testApplicationBuilder is IAsyncCleanableExtension cleanableBuilder)
                {
                    await cleanableBuilder.CleanupAsync().ConfigureAwait(false);
                }
            }
            catch (Exception disposeException)
            {
                AddSecondaryExceptionOrThrow(exception, nameof(IAsyncCleanableExtension.CleanupAsync), disposeException);
            }

            throw;
        }
    }

    private static void AddSecondaryExceptionOrThrow(Exception primaryException, string operation, Exception secondaryException)
    {
        try
        {
            if (primaryException.Data is { IsReadOnly: false } data)
            {
                data[operation] = secondaryException;
                return;
            }
        }
        catch (Exception)
        {
            // An exception can expose an IDictionary implementation that rejects reads or writes.
        }

        throw new AggregateException(
            $"{operation} failed while handling another exception.",
            primaryException,
            secondaryException);
    }
}
