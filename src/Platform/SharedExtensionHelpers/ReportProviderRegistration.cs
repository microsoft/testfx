// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Extensions.TestHost;

namespace Microsoft.Testing.Extensions;

internal static class ReportProviderRegistration
{
    /// <summary>
    /// Registers a report generator as both a data consumer and a test session lifetime handler, along with its
    /// command-line options provider, applying the shared <see cref="TestApplicationBuilder"/> guard.
    /// </summary>
    /// <typeparam name="TGenerator">The report generator type.</typeparam>
    /// <param name="builder">The test application builder.</param>
    /// <param name="invalidBuilderTypeErrorMessage">
    /// The error message used when <paramref name="builder"/> is not a <see cref="TestApplicationBuilder"/>.
    /// </param>
    /// <param name="commandLine">The command-line options provider associated with the report.</param>
    /// <param name="generatorFactory">The factory that creates the report generator from the service provider.</param>
    public static void AddReportProvider<TGenerator>(
        ITestApplicationBuilder builder,
        string invalidBuilderTypeErrorMessage,
        ICommandLineOptionsProvider commandLine,
        Func<IServiceProvider, TGenerator> generatorFactory)
        where TGenerator : class, IDataConsumer, ITestSessionLifetimeHandler
    {
        if (builder is not TestApplicationBuilder)
        {
            throw new InvalidOperationException(invalidBuilderTypeErrorMessage);
        }

        var compositeReportGenerator = new CompositeExtensionFactory<TGenerator>(generatorFactory);

        builder.TestHost.AddDataConsumer(compositeReportGenerator);
        builder.TestHost.AddTestSessionLifetimeHandler(compositeReportGenerator);

        builder.CommandLine.AddProvider(() => commandLine);
    }
}
