// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Hosts;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHostControllers;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
[UnsupportedOSPlatform("browser")]
public sealed class TestHostBuilderTests
{
    [TestMethod]
    public async Task ConnectToTestHostProcessMonitorIfAvailableAsync_MissingPipeName_ReportsPidQualifiedEnvironmentVariable()
    {
        const int testHostControllerPid = 123456789;
        string pipeEnvironmentVariable = $"{EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_PIPENAME}_{testHostControllerPid}";
        SystemEnvironment environment = new();
        string? previousPipeName = environment.GetEnvironmentVariable(pipeEnvironmentVariable);
        environment.SetEnvironmentVariable(pipeEnvironmentVariable, null);

        try
        {
            MethodInfo method = typeof(TestHostBuilder).GetMethod("ConnectToTestHostProcessMonitorIfAvailableAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
            TestHostControllerInfo testHostControllerInfo = new(new CommandLineParseResult(
                null,
                [new CommandLineParseOption(PlatformCommandLineProvider.TestHostControllerPIDOptionKey, [testHostControllerPid.ToString(CultureInfo.InvariantCulture)])],
                []));
            CurrentTestApplicationModuleInfo testApplicationModuleInfo = new(environment, new SystemProcessHandler());
            AggregatedConfiguration configuration = new([], testApplicationModuleInfo, new SystemFileSystem(), environment, new(null, [], []));

            InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => ConnectToTestHostProcessMonitorIfAvailableAsync(method, testHostControllerInfo, configuration, environment));

            Assert.AreEqual($"Unexpected null pipe name from environment variable '{pipeEnvironmentVariable}'", exception.Message);
        }
        finally
        {
            environment.SetEnvironmentVariable(pipeEnvironmentVariable, previousPipeName);
        }
    }

    private static async Task ConnectToTestHostProcessMonitorIfAvailableAsync(
        MethodInfo method,
        TestHostControllerInfo testHostControllerInfo,
        AggregatedConfiguration configuration,
        SystemEnvironment environment)
    {
        using CTRLPlusCCancellationTokenSource cancellationTokenSource = new();
        var connectTask = (Task)method.Invoke(null, [cancellationTokenSource, new NopLogger(), testHostControllerInfo, configuration, environment])!;
        await connectTask;
    }
}
