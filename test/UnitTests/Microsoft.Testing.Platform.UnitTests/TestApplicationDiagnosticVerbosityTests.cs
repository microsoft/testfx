// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Capabilities;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
[DoNotParallelize]
[UnsupportedOSPlatform("browser")]
public sealed class TestApplicationDiagnosticVerbosityTests
{
    [TestMethod]
    public async Task BuildAsync_DiagnosticVerbosityEnvironmentVariable_IsCaseInsensitive()
    {
        string? previousDiagnostic = Environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DIAGNOSTIC);
        string? previousDiagnosticVerbosity = Environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DIAGNOSTIC_VERBOSITY);

        try
        {
            Environment.SetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DIAGNOSTIC, "1");
            Environment.SetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DIAGNOSTIC_VERBOSITY, "trace");

            string[] args = ["--no-banner", "--internal-testingplatform-skipbuildercheck"];
            ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
            builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_, __) => new MockTestAdapter());

            await builder.BuildAsync();
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DIAGNOSTIC, previousDiagnostic);
            Environment.SetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DIAGNOSTIC_VERBOSITY, previousDiagnosticVerbosity);
        }
    }

    private sealed class MockTestAdapter : ITestFramework
    {
        public ICapability[] Capabilities => [];

        public string Uid => nameof(MockTestAdapter);

        public string Version => "1.0.0";

        public string DisplayName => nameof(MockTestAdapter);

        public string Description => string.Empty;

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context) => Task.FromResult(new CreateTestSessionResult { IsSuccess = true });

        public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context) => Task.FromResult(new CloseTestSessionResult { IsSuccess = true });

        public Task ExecuteRequestAsync(ExecuteRequestContext context) => Task.CompletedTask;
    }
}
