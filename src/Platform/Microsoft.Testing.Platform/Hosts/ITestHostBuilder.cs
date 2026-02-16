// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Internal.Framework;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.TestHostOrchestrator;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Telemetry;
using Microsoft.Testing.Platform.TestHost;
using Microsoft.Testing.Platform.TestHostControllers;
using Microsoft.Testing.Platform.Tools;

namespace Microsoft.Testing.Platform.Hosts;

internal interface ITestHostBuilder
{
    ITestFrameworkManager? TestFramework { get; set; }

    ITestHostManager TestHost { get; }

    IConfigurationManager Configuration { get; }

    ILoggingManager Logging { get; }

    ICommandLineManager CommandLine { get; }

    ITestHostControllersManager TestHostControllers { get; }

    ITestHostOrchestratorManager TestHostOrchestratorManager { get; }

    ITelemetryManager Telemetry { get; }

    IToolsManager Tools { get; }

    Task<IHost> BuildAsync(ApplicationLoggingState loggingState, TestApplicationOptions testApplicationOptions, IUnhandledExceptionsHandler unhandledExceptionsHandler, DateTimeOffset createBuilderStart);
}
