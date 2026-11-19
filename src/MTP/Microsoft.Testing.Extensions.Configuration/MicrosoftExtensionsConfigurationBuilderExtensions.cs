// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Configuration;
using Microsoft.Testing.Platform.Builder;

using MelIConfiguration = Microsoft.Extensions.Configuration.IConfiguration;

namespace Microsoft.Testing.Extensions;

/// <summary>
/// Extension methods on <see cref="ITestApplicationBuilder"/> for importing configuration from
/// <c>Microsoft.Extensions.Configuration</c>.
/// </summary>
/// <remarks>
/// This API is experimental. It may change, break, or be removed at any time without notice.
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public static class MicrosoftExtensionsConfigurationBuilderExtensions
{
    /// <summary>
    /// Adds a snapshot of an externally owned <c>Microsoft.Extensions.Configuration</c>
    /// configuration to the Microsoft Testing Platform configuration pipeline.
    /// </summary>
    /// <param name="builder">The test application builder.</param>
    /// <param name="configuration">The external configuration to snapshot when the test application is built.</param>
    /// <param name="order">
    /// The source order. Lower values have higher precedence. The default value, <c>2</c>, places
    /// the snapshot after MTP command-line and environment-variable sources and before
    /// <c>testconfig.json</c>.
    /// </param>
    /// <returns>The same <see cref="ITestApplicationBuilder"/> for chaining.</returns>
    /// <remarks>
    /// <para>
    /// The snapshot is read-only and does not propagate later changes or reload notifications.
    /// The caller retains ownership of <paramref name="configuration"/>.
    /// </para>
    /// <para>
    /// Registration is process-local. The configuration object is not propagated to separately
    /// launched test host or controller processes.
    /// </para>
    /// </remarks>
    public static ITestApplicationBuilder AddMicrosoftExtensionsConfigurationSnapshot(
        this ITestApplicationBuilder builder,
        MelIConfiguration configuration,
        int order = 2)
    {
        _ = builder ?? throw new ArgumentNullException(nameof(builder));
        _ = configuration ?? throw new ArgumentNullException(nameof(configuration));

        builder.Configuration.AddConfigurationSource(
            () => new MicrosoftExtensionsConfigurationSnapshotSource(configuration, order));

        return builder;
    }
}
