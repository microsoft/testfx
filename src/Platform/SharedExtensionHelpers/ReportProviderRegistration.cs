// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions;

#pragma warning disable RS0051 // Reporter registration plumbing is shared-source implementation detail, not package API.

internal static class ReportProviderRegistration
{
    /// <summary>
    /// Registers a report generator as both a data consumer and a test session lifetime handler, along with its
    /// command-line options provider, applying the shared <c>TestApplicationBuilder</c> implementation guard.
    /// </summary>
    /// <typeparam name="TGenerator">The report generator type.</typeparam>
    /// <typeparam name="TCapturedTestResult">The reporter-specific durable result DTO.</typeparam>
    /// <param name="builder">The test application builder.</param>
    /// <param name="invalidBuilderTypeErrorMessage">
    /// The error message used when <paramref name="builder"/> is not a <c>TestApplicationBuilder</c>.
    /// </param>
    /// <param name="optionName">The command-line option that enables the reporter.</param>
    /// <param name="journalEnvironmentVariableName">The environment variable used to pass the journal path to the child.</param>
    /// <param name="commandLineFactory">The factory that creates the command-line options provider associated with the report.</param>
    /// <param name="generatorFactory">The factory that creates the report generator from the service provider.</param>
    /// <param name="recoveredGeneratorFactory">The factory that creates a controller-side generator from recovered metadata.</param>
    /// <param name="journalDeserializer">The source-generated deserializer for reporter journal records.</param>
    public static void AddReportProvider<TGenerator, TCapturedTestResult>(
        ITestApplicationBuilder builder,
        string invalidBuilderTypeErrorMessage,
        string optionName,
        string journalEnvironmentVariableName,
        Func<ICommandLineOptionsProvider> commandLineFactory,
        Func<IServiceProvider, TGenerator> generatorFactory,
        Func<IServiceProvider, RecoveredReportMetadata, TGenerator> recoveredGeneratorFactory,
        Func<string, ReportJournalRecord<TCapturedTestResult>?> journalDeserializer)
        where TGenerator : ReportGeneratorBase<TGenerator, TCapturedTestResult>
        where TCapturedTestResult : class
    {
        if (builder is not TestApplicationBuilder)
        {
            throw new InvalidOperationException(invalidBuilderTypeErrorMessage);
        }

        var compositeReportGenerator = new CompositeExtensionFactory<TGenerator>(generatorFactory);

        builder.TestHost.AddDataConsumer(compositeReportGenerator);
        builder.TestHost.AddTestSessionLifetimeHandler(compositeReportGenerator);

        if (ReportControllerMode.IsSupported)
        {
            var journal = new ReportJournalConfiguration(journalEnvironmentVariableName);
            builder.TestHostControllers.AddEnvironmentVariableProvider(serviceProvider =>
                new ReportJournalEnvironmentVariableProvider(
                    serviceProvider.GetCommandLineOptions(),
                    serviceProvider.GetConfiguration(),
                    serviceProvider.GetRequiredService<IFileSystem>(),
                    optionName,
                    journal));
            builder.TestHostControllers.AddProcessLifetimeHandler(serviceProvider =>
                new ReportProcessLifetimeHandler<TGenerator, TCapturedTestResult>(
                    serviceProvider,
                    optionName,
                    journal,
                    recoveredGeneratorFactory,
                    journalDeserializer));
        }

        ICommandLineOptionsProvider commandLine = commandLineFactory();
        builder.CommandLine.AddProvider(() => commandLine);
    }
}

#pragma warning restore RS0051
