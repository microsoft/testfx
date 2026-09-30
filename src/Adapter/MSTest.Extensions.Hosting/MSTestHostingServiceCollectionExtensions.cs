// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Testing.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for enabling application-host service injection into MSTest test classes.
/// </summary>
/// <remarks>
/// This API is experimental. It may change, break, or be removed at any time without notice.
/// </remarks>
[Experimental("MSTESTEXP", UrlFormat = "https://aka.ms/mstest/diagnostics#{0}")]
public static class MSTestHostingServiceCollectionExtensions
{
    /// <summary>
    /// Enables construction of MSTest test classes from the application host's service provider.
    /// </summary>
    /// <param name="services">The application host service collection.</param>
    /// <returns>The supplied service collection.</returns>
    /// <remarks>
    /// This API is experimental. It may change, break, or be removed at any time without notice.
    /// </remarks>
    public static IServiceCollection AddMSTestTestClassInjection(this IServiceCollection services)
    {
        _ = services ?? throw new ArgumentNullException(nameof(services));

        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITestingPlatformBuilderConfigurator, MSTestTestClassBuilderConfigurator>());
        return services;
    }
}
