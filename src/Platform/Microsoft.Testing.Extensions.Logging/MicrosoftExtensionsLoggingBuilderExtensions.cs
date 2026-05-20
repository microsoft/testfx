// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;
using Microsoft.Testing.Extensions.Logging;
using Microsoft.Testing.Platform.Builder;

using MelILoggerFactory = Microsoft.Extensions.Logging.ILoggerFactory;
using MelLoggerFactory = Microsoft.Extensions.Logging.LoggerFactory;

namespace Microsoft.Testing.Extensions;

/// <summary>
/// Extension methods on <see cref="ITestApplicationBuilder"/> for forwarding the
/// Microsoft Testing Platform diagnostic logs to <c>Microsoft.Extensions.Logging</c> providers.
/// </summary>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public static class MicrosoftExtensionsLoggingBuilderExtensions
{
    /// <summary>
    /// Forwards every diagnostic log message produced by Microsoft Testing Platform and its
    /// extensions to <c>Microsoft.Extensions.Logging</c> providers configured by the supplied delegate.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A new <see cref="MelILoggerFactory"/> is created internally for each call and disposed when the
    /// test application terminates. The minimum level is initialized from the platform's effective
    /// diagnostic level so log volume is bounded by the platform configuration; per-category filters in
    /// the <see cref="ILoggingBuilder"/> can only narrow this set, never widen it.
    /// </para>
    /// <para>
    /// When the platform's effective level is <see cref="Microsoft.Testing.Platform.Logging.LogLevel.None"/>
    /// (the default when <c>--diagnostic</c> is not enabled), the supplied <paramref name="configure"/>
    /// delegate is not invoked and no <see cref="MelILoggerFactory"/> is created, so expensive sinks
    /// (network, file, gRPC) are not initialized for runs that will never emit a log.
    /// </para>
    /// <para>
    /// Microsoft Testing Platform's <see cref="Microsoft.Testing.Platform.Logging.ILogger"/> has no notion
    /// of <see cref="Microsoft.Extensions.Logging.EventId"/>; messages forwarded through this bridge are
    /// logged with <c>EventId.None</c>.
    /// </para>
    /// <para>
    /// This extension is additive: it does not replace the platform's built-in <c>--diagnostic</c>
    /// file logger. When <c>--diagnostic</c> is enabled, messages are written to both the diagnostic
    /// file and the configured <c>Microsoft.Extensions.Logging</c> providers.
    /// </para>
    /// </remarks>
    /// <param name="builder">The test application builder.</param>
    /// <param name="configure">A delegate that configures the <see cref="ILoggingBuilder"/>.</param>
    /// <returns>The same <see cref="ITestApplicationBuilder"/> for chaining.</returns>
    public static ITestApplicationBuilder AddMicrosoftExtensionsLogging(
        this ITestApplicationBuilder builder,
        Action<ILoggingBuilder> configure)
    {
        _ = builder ?? throw new ArgumentNullException(nameof(builder));
        _ = configure ?? throw new ArgumentNullException(nameof(configure));

        builder.Logging.AddProvider((mtpLevel, _) =>
        {
            if (mtpLevel == Microsoft.Testing.Platform.Logging.LogLevel.None)
            {
                // No log message will ever flow; avoid constructing the user's logging pipeline.
                return NopLoggerProvider.Instance;
            }

            MelILoggerFactory factory = MelLoggerFactory.Create(loggingBuilder =>
            {
                loggingBuilder.SetMinimumLevel(LogLevelMapper.ToMicrosoftExtensions(mtpLevel));
                configure(loggingBuilder);
            });
            return new MicrosoftExtensionsLoggingProvider(factory, ownsFactory: true);
        });

        return builder;
    }

    /// <summary>
    /// Forwards every diagnostic log message produced by Microsoft Testing Platform and its
    /// extensions to a caller-owned <see cref="MelILoggerFactory"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The factory's lifetime is owned by the caller; the bridge will not dispose it.
    /// This overload is intended for scenarios where the same <see cref="MelILoggerFactory"/>
    /// instance is shared with other components, such as the system under test.
    /// </para>
    /// <para>
    /// When the platform's effective level is <see cref="Microsoft.Testing.Platform.Logging.LogLevel.None"/>,
    /// the bridge becomes a no-op and no calls are made on <paramref name="loggerFactory"/>.
    /// </para>
    /// </remarks>
    /// <param name="builder">The test application builder.</param>
    /// <param name="loggerFactory">A <see cref="MelILoggerFactory"/> that the platform should forward logs to.</param>
    /// <returns>The same <see cref="ITestApplicationBuilder"/> for chaining.</returns>
    public static ITestApplicationBuilder AddMicrosoftExtensionsLogging(
        this ITestApplicationBuilder builder,
        MelILoggerFactory loggerFactory)
    {
        _ = builder ?? throw new ArgumentNullException(nameof(builder));
        _ = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));

        builder.Logging.AddProvider((mtpLevel, _) => mtpLevel == Microsoft.Testing.Platform.Logging.LogLevel.None
            ? NopLoggerProvider.Instance
            : new MicrosoftExtensionsLoggingProvider(loggerFactory, ownsFactory: false));

        return builder;
    }
}
