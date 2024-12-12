// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Telemetry;
using Microsoft.Testing.Platform.TestHostControllers;

namespace Microsoft.Testing.Platform.Services;

/// <summary>
/// Extension methods for getting services from an <see cref="IServiceProvider" />.
/// </summary>
public static class ServiceProviderExtensions
{
    /// <summary>
    /// Gets the required service of type <typeparamref name="TService"/> from the <see cref="IServiceProvider"/>.
    /// </summary>
    /// <typeparam name="TService">The type of the service.</typeparam>
    /// <param name="provider">The service provider.</param>
    /// <returns>The required service of type <typeparamref name="TService"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="provider"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the required service is not found.</exception>
    public static TService GetRequiredService<TService>(this IServiceProvider provider)
        where TService : notnull
    {
        Guard.NotNull(provider);

        object? service = ((ServiceProvider)provider).GetService(typeof(TService));
        ApplicationStateGuard.Ensure(service is not null, string.Format(CultureInfo.InvariantCulture, PlatformResources.ServiceProviderCannotFindServiceErrorMessage, typeof(TService)));

        return (TService)service;
    }

    /// <summary>
    /// Gets the service of type <typeparamref name="TService"/> from the <see cref="IServiceProvider"/>.
    /// </summary>
    /// <typeparam name="TService">The type of the service.</typeparam>
    /// <param name="provider">The service provider.</param>
    /// <returns>The service of type <typeparamref name="TService"/> or null if not found.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="provider"/> is null.</exception>
    public static TService? GetService<TService>(this IServiceProvider provider)
        where TService : class
    {
        Guard.NotNull(provider);

        return ((ServiceProvider)provider).GetService(typeof(TService)) as TService;
    }

    /// <summary>
    /// Gets the service of type <typeparamref name="TService"/> from the <see cref="IServiceProvider"/>.
    /// </summary>
    /// <typeparam name="TService">The type of the service.</typeparam>
    /// <param name="provider">The service provider.</param>
    /// <returns>The services of type <typeparamref name="TService"/> or an empty collection if none found.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="provider"/> is null.</exception>
    public static IEnumerable<TService> GetServices<TService>(this IServiceProvider provider)
        where TService : class
    {
        Guard.NotNull(provider);

        return ((ServiceProvider)provider).GetServicesInternal(typeof(TService)).OfType<TService>();
    }

    /// <summary>
    /// Gets the message bus from the <see cref="IServiceProvider"/>.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <returns>The message bus.</returns>
    public static IMessageBus GetMessageBus(this IServiceProvider serviceProvider)
        => serviceProvider.GetRequiredService<IMessageBus>();

    /// <summary>
    /// Gets the configuration from the <see cref="IServiceProvider"/>.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <returns>The configuration.</returns>
    public static IConfiguration GetConfiguration(this IServiceProvider serviceProvider)
        => serviceProvider.GetRequiredService<IConfiguration>();

    /// <summary>
    /// Gets the command line options from the <see cref="IServiceProvider"/>.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <returns>The command line options.</returns>
    public static ICommandLineOptions GetCommandLineOptions(this IServiceProvider serviceProvider)
        => serviceProvider.GetRequiredService<ICommandLineOptions>();

    /// <summary>
    /// Gets the logger factory from the <see cref="IServiceProvider"/>.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <returns>The logger factory.</returns>
    public static ILoggerFactory GetLoggerFactory(this IServiceProvider serviceProvider)
        => serviceProvider.GetRequiredService<ILoggerFactory>();

    /// <summary>
    /// Gets the output device from the <see cref="IServiceProvider"/>.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <returns>The output device.</returns>
    public static IOutputDevice GetOutputDevice(this IServiceProvider serviceProvider)
        => serviceProvider.GetRequiredServiceInternal<IOutputDevice>();

    /// <summary>
    /// Gets the IClientInfo from the <see cref="IServiceProvider"/>.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <returns>The IClientInfo object.</returns>
    [Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
    public static IClientInfo GetClientInfo(this IServiceProvider serviceProvider)
        => serviceProvider.GetRequiredServiceInternal<IClientInfo>();

    // Internals extensions
    internal static TService GetRequiredServiceInternal<TService>(this IServiceProvider provider)
        where TService : notnull
    {
        Guard.NotNull(provider);

        object? service = ((ServiceProvider)provider).GetServiceInternal(typeof(TService));
        ApplicationStateGuard.Ensure(service is not null, string.Format(CultureInfo.InvariantCulture, PlatformResources.ServiceProviderCannotFindServiceErrorMessage, typeof(TService)));

        return (TService)service;
    }

    internal static TService? GetServiceInternal<TService>(this IServiceProvider provider)
        where TService : class
    {
        Guard.NotNull(provider);

        return ((ServiceProvider)provider).GetServiceInternal(typeof(TService)) as TService;
    }

    internal static IEnumerable<TService> GetServicesInternal<TService>(this IServiceProvider provider)
        where TService : notnull
    {
        Guard.NotNull(provider);

        return ((ServiceProvider)provider).GetServicesInternal(typeof(TService)).Cast<TService>();
    }

    internal static ITestSessionContext GetTestSessionContext(this IServiceProvider serviceProvider)
        => serviceProvider.GetRequiredServiceInternal<ITestSessionContext>();

    internal static IClock GetSystemClock(this IServiceProvider serviceProvider)
        => serviceProvider.GetRequiredServiceInternal<IClock>();

    internal static ITask GetTask(this IServiceProvider serviceProvider)
        => serviceProvider.GetRequiredServiceInternal<ITask>();

    internal static IPlatformOutputDevice GetPlatformOutputDevice(this IServiceProvider serviceProvider)
        => serviceProvider.GetRequiredServiceInternal<IPlatformOutputDevice>();

    internal static IEnvironment GetEnvironment(this IServiceProvider serviceProvider)
        => serviceProvider.GetRequiredServiceInternal<IEnvironment>();

    internal static ITestApplicationModuleInfo GetTestApplicationModuleInfo(this IServiceProvider serviceProvider)
        => serviceProvider.GetRequiredServiceInternal<ITestApplicationModuleInfo>();

    internal static ITestHostControllerInfo GetTestHostControllerInfo(this IServiceProvider serviceProvider)
        => serviceProvider.GetRequiredServiceInternal<ITestHostControllerInfo>();

    internal static IProcessHandler GetProcessHandler(this IServiceProvider serviceProvider)
        => serviceProvider.GetRequiredServiceInternal<IProcessHandler>();

    internal static IClock GetClock(this IServiceProvider serviceProvider)
        => serviceProvider.GetRequiredServiceInternal<IClock>();

    internal static BaseMessageBus GetBaseMessageBus(this IServiceProvider serviceProvider)
        => serviceProvider.GetRequiredServiceInternal<BaseMessageBus>();

    internal static IConsole GetConsole(this IServiceProvider serviceProvider)
        => serviceProvider.GetRequiredServiceInternal<IConsole>();

    internal static IRuntimeFeature GetRuntimeFeature(this IServiceProvider serviceProvider)
        => serviceProvider.GetRequiredServiceInternal<IRuntimeFeature>();

    internal static IAsyncMonitorFactory GetAsyncMonitorFactory(this IServiceProvider serviceProvider)
        => serviceProvider.GetRequiredServiceInternal<IAsyncMonitorFactory>();

    internal static ITestApplicationProcessExitCode GetTestApplicationProcessExitCode(this IServiceProvider serviceProvider)
        => serviceProvider.GetRequiredServiceInternal<ITestApplicationProcessExitCode>();

    internal static IMonitor GetMonitor(this IServiceProvider serviceProvider)
        => serviceProvider.GetRequiredServiceInternal<IMonitor>();

    internal static ITestApplicationCancellationTokenSource GetTestApplicationCancellationTokenSource(this IServiceProvider serviceProvider)
        => serviceProvider.GetRequiredServiceInternal<ITestApplicationCancellationTokenSource>();

    internal static ITelemetryInformation GetTelemetryInformation(this IServiceProvider serviceProvider)
        => serviceProvider.GetRequiredServiceInternal<ITelemetryInformation>();

    internal static ITelemetryCollector GetTelemetryCollector(this IServiceProvider serviceProvider)
        => serviceProvider.GetRequiredServiceInternal<ITelemetryCollector>();

    internal static ITestFramework GetTestFramework(this IServiceProvider serviceProvider)
        => serviceProvider.GetRequiredServiceInternal<ITestFramework>();

    internal static ITestFrameworkInvoker GetTestAdapterInvoker(this IServiceProvider serviceProvider)
        => serviceProvider.GetRequiredServiceInternal<ITestFrameworkInvoker>();

    internal static IUnhandledExceptionsPolicy GetUnhandledExceptionsPolicy(this IServiceProvider serviceProvider)
        => serviceProvider.GetRequiredServiceInternal<IUnhandledExceptionsPolicy>();

    internal static ITestExecutionRequestFactory GetTestExecutionRequestFactory(this IServiceProvider serviceProvider)
        => serviceProvider.GetRequiredServiceInternal<ITestExecutionRequestFactory>();

    internal static IFileSystem GetFileSystem(this IServiceProvider serviceProvider)
        => serviceProvider.GetRequiredServiceInternal<IFileSystem>();

    internal static ITestFrameworkCapabilities GetTestFrameworkCapabilities(this IServiceProvider serviceProvider)
        => serviceProvider.GetRequiredServiceInternal<ITestFrameworkCapabilities>();

    internal static CommandLineParseResult GetCommandLineParseResult(this IServiceProvider serviceProvider)
        => serviceProvider.GetRequiredServiceInternal<ApplicationLoggingState>().CommandLineParseResult;

    internal static IPlatformInformation GetPlatformInformation(this IServiceProvider serviceProvider)
        => serviceProvider.GetRequiredServiceInternal<IPlatformInformation>();

    internal static IFileLoggerInformation? GetFileLoggerInformation(this IServiceProvider serviceProvider)
        => serviceProvider.GetServiceInternal<IFileLoggerInformation>();
}
