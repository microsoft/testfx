// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Framework;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Extensions.CommandLine;
using Microsoft.Testing.Extensions.TestHost;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.TestInfrastructure;

// DebuggerUtility.AttachVSToCurrentProcess();
ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
builder.TestHost.AddTestApplicationLifecycleCallbacks(sp => new GlobalTasks(sp.GetCommandLineOptions()));
builder.AddTestFramework(new Microsoft.Testing.Platform.UnitTests.SourceGeneratedTestNodesBuilder());

var commandLine = new FakeTrxReportGeneratorCommandLine();
builder.CommandLine.AddProvider(() => commandLine);

// Custom suite tools
CompositeExtensionFactory<SlowestTestsConsumer> slowestTestCompositeServiceFactory = new(_ => new SlowestTestsConsumer());
builder.TestHost.AddDataConsumer(slowestTestCompositeServiceFactory);
builder.TestHost.AddTestSessionLifetimeHandle(slowestTestCompositeServiceFactory);
ITestApplication app = await builder.BuildAsync();
return await app.RunAsync();

internal sealed class GlobalTasks : ITestApplicationLifecycleCallbacks
{
    private readonly ICommandLineOptions _commandLineOptions;

    public GlobalTasks(ICommandLineOptions commandLineOptions)
    {
        _commandLineOptions = commandLineOptions;
    }

    public string Uid => nameof(GlobalTasks);

    public string Version => "1.0.0";

    public string DisplayName => string.Empty;

    public string Description => string.Empty;

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public async Task AfterRunAsync(int returnValue, CancellationToken cancellationToken)
    {
        // Check if any tests are missing that were supposed to run.
#if NETCOREAPP
        TestsRunWatchDog.BaselineFile = Path.Combine(AppContext.BaseDirectory, "testsbaseline.txt");
#else
        TestsRunWatchDog.BaselineFile = Path.Combine(AppContext.BaseDirectory, "testsbaseline.netfx.txt");
#endif
        await TestsRunWatchDog.Verify(skip: _commandLineOptions.IsServerMode(), fixBaseLine: true);
    }

    public Task BeforeRunAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class FakeTrxReportGeneratorCommandLine : ICommandLineOptionsProvider
{
    public const string IsTrxReportEnabled = "report-trx";
    public const string TrxReportFileName = "report-trx-filename";

    public string Uid => "fake trx";

    public string Version => "1.0.0";

    public string DisplayName => "Fake trx";

    public string Description => "Fake trx";

    public CommandLineOption[] GetCommandLineOptions()
       => new CommandLineOption[]
        {
            new(IsTrxReportEnabled, $"Generate the TRX report.", ArgumentArity.ZeroOrOne, false),
            new(TrxReportFileName, $"Name of the generated TRX report file.", ArgumentArity.ZeroOrOne, false),
        };

    public Task<bool> IsEnabledAsync()
    {
        return Task.FromResult(true);
    }

    public bool IsValidConfiguration(ICommandLineOptions commandLineOptions, out string? errorMessage)
    {
        errorMessage = null;
        return true;
    }

    public bool OptionArgumentsAreValid(CommandLineOption commandOption, string[] arguments, out string? errorMessage)
    {
        errorMessage = null;
        return true;
    }
}
