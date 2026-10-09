// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.PackagedApp.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.PackagedApp;

internal static class PackagedAppControllerArguments
{
    internal const string Prefix = "--internal-packagedapp-controller-v1";
    internal const string BootstrapFileName = "mstest-appmodel-controller.bootstrap";
    internal const string TargetEnvironmentVariable = "TESTINGPLATFORM_PACKAGEDAPP_TARGET";
    internal const string ExtensionsEnvironmentVariable = "MSTEST_APPMODEL_CONTROLLER_EXTENSIONS";
    internal const string NativePipeEnvironmentVariable = "MSTEST_APPMODEL_CONTROLLER_DOTNETTEST_PIPE";
    internal const string ExecutionIdEnvironmentVariable = "TESTINGPLATFORM_DOTNETTEST_EXECUTIONID";

    internal static (string[] Arguments, bool IsInformationalRequest) Configure(
        string[] arguments,
        Func<string, string?> getEnvironmentVariable,
        Action<string, string?> setEnvironmentVariable,
        string[]? bootstrapArguments = null)
    {
        if (bootstrapArguments is not null)
        {
            if (bootstrapArguments.Length != 3 || bootstrapArguments[0] != Prefix)
            {
                throw new FormatException(ExtensionResources.PackagedAppControllerInvalidBootstrap);
            }

            arguments = [.. bootstrapArguments, .. arguments];
        }

        if (arguments.Length == 0 || !arguments[0].StartsWith("--internal-packagedapp-controller", StringComparison.Ordinal))
        {
            // Retry can relaunch a controller using its already-consumed argument array.
            return (arguments, false);
        }

        if (arguments[0] != Prefix || arguments.Length < 3
            || !Path.IsPathFullyQualified(arguments[1]) || arguments[2].Trim().Length == 0)
        {
            throw new FormatException(ExtensionResources.PackagedAppControllerInvalidBootstrap);
        }

        string[] remaining = arguments[3..];
        int transportIndex = -1;
        for (int i = 0; i < remaining.Length; i++)
        {
            string argument = remaining[i];
            string option = argument.StartsWith("-", StringComparison.Ordinal) ? argument.TrimStart('-') : string.Empty;
            if (option == "server")
            {
                if (argument != "--server" || transportIndex != -1 || i + 3 >= remaining.Length
                    || remaining[i + 1] != "dotnettestcli"
                    || remaining[i + 2] != "--dotnet-test-pipe"
                    || remaining[i + 3].Trim().Length == 0
                    || remaining[i + 3].StartsWith("--", StringComparison.Ordinal))
                {
                    throw new FormatException(ExtensionResources.PackagedAppControllerInvalidTransport);
                }

                transportIndex = i;
                i += 3;
            }
            else if (option.StartsWith("dotnet-test-", StringComparison.Ordinal)
                || option.StartsWith("server=", StringComparison.Ordinal)
                || option.StartsWith("server:", StringComparison.Ordinal))
            {
                throw new FormatException(ExtensionResources.PackagedAppControllerInvalidTransport);
            }
        }

        setEnvironmentVariable(TargetEnvironmentVariable, Path.GetFullPath(arguments[1]));
        setEnvironmentVariable(ExtensionsEnvironmentVariable, arguments[2]);
        bool isRestart = PackagedAppConnectBackHandshake.TryGetHandshakeId(remaining) is not null;
        if (transportIndex >= 0 || !isRestart)
        {
            setEnvironmentVariable(NativePipeEnvironmentVariable, transportIndex < 0 ? null : remaining[transportIndex + 3]);
        }

        if (transportIndex >= 0 && getEnvironmentVariable(ExecutionIdEnvironmentVariable) is null or "")
        {
            setEnvironmentVariable(ExecutionIdEnvironmentVariable, Guid.NewGuid().ToString("N"));
        }

        // Only the activated host connects to the SDK: an external controller may run on a different
        // architecture/runtime, which older SDKs reject when comparing every process handshake.
        string[] controllerArguments = transportIndex < 0
            ? remaining
            : [.. remaining[..transportIndex], .. remaining[(transportIndex + 4)..]];
        CommandLineParseResult parseResult = CommandLineParser.Parse(controllerArguments, new SystemEnvironment());
        bool informationalRequest = parseResult.IsOptionSet("help")
            || parseResult.IsOptionSet("?")
            || parseResult.IsOptionSet("list-tests");
        return (controllerArguments, informationalRequest);
    }

    internal static IReadOnlyList<string> AddNativeTransport(IReadOnlyList<string> arguments, string? pipeName)
        => pipeName is null or ""
            ? arguments
            : [.. arguments, "--server", "dotnettestcli", "--dotnet-test-pipe", pipeName];

    internal static void ValidateNativeApplication(bool runsInAppContainer, IReadOnlyList<string> arguments)
    {
        if (runsInAppContainer && arguments.Contains("--dotnet-test-pipe"))
        {
            throw new InvalidOperationException(ExtensionResources.PackagedAppControllerNativeAppContainerNotSupported);
        }
    }
}
