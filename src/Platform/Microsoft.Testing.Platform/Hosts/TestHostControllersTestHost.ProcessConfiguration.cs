// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHostControllers;

namespace Microsoft.Testing.Platform.Hosts;

internal sealed partial class TestHostControllersTestHost
{
    private async Task<(ProcessStartInfo ProcessStartInfo, IReadOnlyList<string> PartialCommandLine)?> PrepareProcessConfigurationAsync(
        ExecutableInfo executableInfo,
        int currentPid,
        string processIdString,
        string processCorrelationId,
        NamedPipeServer testHostControllerIpc,
        TestHostControllerCancellationServer testHostControllerCancellationServer,
        IEnvironment environment,
        ProxyOutputDevice outputDevice,
        CancellationToken cancellationToken)
    {
        List<string> partialCommandLine =
        [
            .. executableInfo.Arguments,
            $"--{PlatformCommandLineProvider.TestHostControllerPIDOptionKey}",
            processIdString
        ];

#if NET8_0_OR_GREATER
        // On net8.0+, we can pass the arguments as a collection directly to ProcessStartInfo.
        // When passing the collection, it's expected to be unescaped, so we pass what we have directly.
        IEnumerable<string> arguments = partialCommandLine;
#else
        // Current target framework (.NET Framework and .NET Standard 2.0) only supports arguments as a single string.
        // In this case, escaping is essential. For example, one of the arguments could already contain spaces.
        // PasteArguments is borrowed from dotnet/runtime.
        var builder = new StringBuilder();
        foreach (string arg in partialCommandLine)
        {
            PasteArguments.AppendArgument(builder, arg);
        }

        string arguments = builder.ToString();
#endif

        ProcessStartInfo processStartInfo = new(
            executableInfo.FilePath,
            arguments)
        {
            EnvironmentVariables =
            {
                { $"{EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_CORRELATIONID}_{currentPid}", processCorrelationId },
                { $"{EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_PARENTPID}_{currentPid}", processIdString },
                { $"{EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_SKIPEXTENSION}_{currentPid}", "1" },
                { $"{EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_PIPENAME}_{currentPid}", testHostControllerIpc.PipeName.Name },
                { $"{EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_CONTROLPIPENAME}_{currentPid}", testHostControllerCancellationServer.PipeName },
            },
            UseShellExecute = false,
        };

        List<IDataConsumer> dataConsumersBuilder = [.. _testHostsInformation.DataConsumer];
        if (ServiceProvider.GetService<TestCoverageCapabilities>() is { } coverageCapabilities)
        {
            coverageCapabilities.RegisterProducers(dataConsumersBuilder);
        }

        // Register the coverage result consumer so that coverage messages published by
        // ITestHostProcessLifetimeHandler extensions in this (controller) process are tracked.
        // This is the same instance later read by the coverage threshold exit code check.
        if (ServiceProvider.GetService<TestCoverageResult>() is { } testCoverageResult)
        {
            dataConsumersBuilder.Add(testCoverageResult);
        }

        // We add the IPlatformOutputDevice after all users extensions.
        IPlatformOutputDevice? display = ServiceProvider.GetServiceInternal<IPlatformOutputDevice>();
        if (display is IDataConsumer dataConsumerDisplay)
        {
            dataConsumersBuilder.Add(dataConsumerDisplay);
        }

        // We register the DotnetTestDataConsumer as last to ensure that it will be the last one to consume the data.
        IPushOnlyProtocol? pushOnlyProtocol = ServiceProvider.GetService<IPushOnlyProtocol>();
        if (pushOnlyProtocol?.IsServerMode == true)
        {
            dataConsumersBuilder.Add(await pushOnlyProtocol.GetDataConsumerAsync().ConfigureAwait(false));
        }

        // If we're in server mode jsonrpc we add as last consumer the PassiveNodeDataConsumer for the attachments.
        // Connect the passive node if it's available
        if (_passiveNode is not null)
        {
            if (await _passiveNode.ConnectAsync().ConfigureAwait(false))
            {
                dataConsumersBuilder.Add(new PassiveNodeDataConsumer(_passiveNode));
            }
            else
            {
                await _logger.LogWarningAsync("PassiveNode was expected to connect but failed").ConfigureAwait(false);
            }
        }

        var concreteMessageBusService = new AsynchronousMessageBus(
            [.. dataConsumersBuilder],
            ServiceProvider.GetTestApplicationCancellationTokenSource(),
            ServiceProvider.GetTask(),
            ServiceProvider.GetLoggerFactory(),
            ServiceProvider.GetEnvironment(),
            ServiceProvider.GetService<IShutdownProgressReporter>());
        await concreteMessageBusService.InitAsync().ConfigureAwait(false);
        ((MessageBusProxy)ServiceProvider.GetMessageBus()).SetBuiltMessageBus(concreteMessageBusService);

        SystemEnvironmentVariableProvider systemEnvironmentVariableProvider = new(environment);
        EnvironmentVariables environmentVariables = new(_loggerFactory)
        {
            CurrentProvider = systemEnvironmentVariableProvider,
        };
        await systemEnvironmentVariableProvider.UpdateAsync(environmentVariables).ConfigureAwait(false);
        await ApplyControllerExtensionPreLaunchAsync(
            _testHostsInformation.LifetimeHandlers,
            _testHostsInformation.EnvironmentVariableProviders,
            environmentVariables,
            cancellationToken).ConfigureAwait(false);

        // Apply the ITestHostEnvironmentVariableProvider
        if (_testHostsInformation.EnvironmentVariableProviders.Length > 0)
        {
            environmentVariables.CurrentProvider = null;

            List<(IExtension, string)> failedValidations = [];
            foreach (ITestHostEnvironmentVariableProvider hostEnvironmentVariableProvider in _testHostsInformation.EnvironmentVariableProviders)
            {
                ValidationResult variableResult = await hostEnvironmentVariableProvider.ValidateTestHostEnvironmentVariablesAsync(environmentVariables).ConfigureAwait(false);
                if (!variableResult.IsValid)
                {
                    failedValidations.Add((hostEnvironmentVariableProvider, variableResult.ErrorMessage));
                }
            }

            if (failedValidations.Count > 0)
            {
                StringBuilder displayErrorMessageBuilder = new();
                StringBuilder logErrorMessageBuilder = new();
                displayErrorMessageBuilder.AppendLine(PlatformResources.GlobalValidationOfTestHostEnvironmentVariablesFailedErrorMessage);
                logErrorMessageBuilder.AppendLine("The following 'ITestHostEnvironmentVariableProvider' providers rejected the final environment variables setup:");
                foreach ((IExtension extension, string errorMessage) in failedValidations)
                {
                    displayErrorMessageBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, PlatformResources.EnvironmentVariableProviderFailedWithError, extension.DisplayName, extension.Uid, errorMessage));
                    displayErrorMessageBuilder.AppendLine(CultureInfo.InvariantCulture, $"Provider '{extension.DisplayName}' (UID: {extension.Uid}) failed with error: {errorMessage}");
                }

                await outputDevice.DisplayAsync(this, new ErrorMessageOutputDeviceData(displayErrorMessageBuilder.ToString()), cancellationToken).ConfigureAwait(false);
                await _logger.LogErrorAsync(logErrorMessageBuilder.ToString()).ConfigureAwait(false);
                return null;
            }

            foreach (EnvironmentVariable envVar in environmentVariables.GetAll())
            {
                processStartInfo.EnvironmentVariables[envVar.Variable] = envVar.Value;
            }
        }

        return (processStartInfo, partialCommandLine);
    }

    internal static async Task ApplyControllerExtensionPreLaunchAsync(
        IReadOnlyList<ITestHostProcessLifetimeHandler> lifetimeHandlers,
        IReadOnlyList<ITestHostEnvironmentVariableProvider> environmentVariableProviders,
        EnvironmentVariables environmentVariables,
        CancellationToken cancellationToken)
    {
        // Servers can qualify their endpoint while starting, so handlers must run before providers publish
        // those endpoints to the child process.
        foreach (ITestHostProcessLifetimeHandler lifetimeHandler in lifetimeHandlers)
        {
            await lifetimeHandler.BeforeTestHostProcessStartAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (ITestHostEnvironmentVariableProvider environmentVariableProvider in environmentVariableProviders)
        {
            environmentVariables.CurrentProvider = environmentVariableProvider;
            await environmentVariableProvider.UpdateAsync(environmentVariables).ConfigureAwait(false);
        }
    }
}
