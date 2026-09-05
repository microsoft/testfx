// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Extensions.Policy.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHostOrchestrator;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHostOrchestrator;

namespace Microsoft.Testing.Extensions.Policy;

[UnsupportedOSPlatform("browser")]
internal sealed partial class RetryOrchestrator : ITestHostExecutionOrchestrator, IOutputDeviceDataProducer, ITestHostControllerConnectionAuthorizationConsumer
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ICommandLineOptions _commandLineOptions;
    private readonly IFileSystem _fileSystem;

    public RetryOrchestrator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _commandLineOptions = _serviceProvider.GetCommandLineOptions();
        _fileSystem = _serviceProvider.GetFileSystem();
    }

    public string Uid => nameof(RetryOrchestrator);

    public string Version => ExtensionVersion.DefaultSemVer;

    public string DisplayName => ExtensionResources.RetryFailedTestsExtensionDisplayName;

    public string Description => ExtensionResources.RetryFailedTestsExtensionDescription;

    public Task<bool> IsEnabledAsync()
        => Task.FromResult(_commandLineOptions.IsOptionSet(RetryCommandLineOptionsProvider.RetryFailedTestsOptionName));

    private string CreateRetriesDirectory(string resultDirectory)
    {
        Exception? lastException = null;
        // Quite arbitrary. Keep trying to create the directory for 10 times.
        for (int i = 0; i < 10; i++)
        {
            string retryRootFolder = Path.Combine(resultDirectory, "Retries", RandomId.Next());
            if (_fileSystem.ExistDirectory(retryRootFolder))
            {
                continue;
            }

            try
            {
                _fileSystem.CreateDirectory(retryRootFolder);
                return retryRootFolder;
            }
            catch (IOException ex)
            {
                lastException = ex;
            }
        }

        if (lastException is not null)
        {
            throw lastException;
        }

        throw new IOException(string.Format(CultureInfo.InvariantCulture, ExtensionResources.FailedToCreateRetryDirectoryBecauseOfCollision, resultDirectory));
    }

    // Copied from HotReloadTestHostTestFrameworkInvoker
    private static bool IsHotReloadEnabled(IEnvironment environment)
        => environment.GetEnvironmentVariable(EnvironmentVariableConstants.DOTNET_WATCH) == "1"
        || environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_HOTRELOAD_ENABLED) == "1";

    internal static async Task LogResponseFileFallbackWarningAsync(
        ILogger logger,
        string[] originalExecutableArguments,
        List<string> finalArguments,
        string generatedResponseFilePath)
    {
        if (originalExecutableArguments.Any(argument => argument.StartsWith("@", StringComparison.Ordinal))
            && !finalArguments.Contains($"@{generatedResponseFilePath}"))
        {
            await logger.LogWarningAsync(
                "Retry arguments could not be regenerated in a response file because an argument contains a literal double quote. "
                + "The retry command line may exceed the operating system limit.").ConfigureAwait(false);
        }
    }
}
