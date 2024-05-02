// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Reflection;
using System.Text;

using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Requests;

namespace TestingPlatformExplorer.TestingFramework;

internal sealed class TestingFramework : ITestFramework, IDataProducer, IDisposable, IOutputDeviceDataProducer
{
    private readonly TestingFrameworkCapabilities _capabilities;
    private readonly ICommandLineOptions _commandLineOptions;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TestingFramework> _logger;
    private readonly IOutputDevice _outputDevice;
    private readonly Assembly[] _assemblies;
    private readonly SemaphoreSlim _dop;
    private readonly string _reportFile = string.Empty;
    private readonly int _dopValue;

    public TestingFramework(
        ITestFrameworkCapabilities capabilities,
        ICommandLineOptions commandLineOptions,
        IConfiguration configuration,
        ILogger<TestingFramework> logger,
        IOutputDevice outputDevice,
        Assembly[] assemblies)
    {
        _capabilities = (TestingFrameworkCapabilities)capabilities;
        _commandLineOptions = commandLineOptions;
        _configuration = configuration;
        _logger = logger;
        _outputDevice = outputDevice;
        _assemblies = assemblies;

        if (_commandLineOptions.TryGetOptionArgumentList(TestingFrameworkCommandLineOptions.DopOption, out string[]? argumentList))
        {
            _dopValue = int.Parse(argumentList[0], CultureInfo.InvariantCulture);
            _dop = new SemaphoreSlim(_dopValue, _dopValue);
        }
        else
        {
            _dop = new SemaphoreSlim(int.MaxValue, int.MaxValue);
        }

        if (_configuration["TestingFramework:DisableParallelism"] == bool.TrueString)
        {
            _dop?.Dispose();
            _dop = new SemaphoreSlim(1, 1);
            _dopValue = 1;
        }

        if (_commandLineOptions.IsOptionSet(TestingFrameworkCommandLineOptions.GenerateReportOption))
        {
            if (_commandLineOptions.TryGetOptionArgumentList(TestingFrameworkCommandLineOptions.ReportFilenameOption, out string[]? reportFile))
            {
                _reportFile = reportFile[0];
            }
        }
    }

    public string Uid => nameof(TestingFramework);

    public string Version => "1.0.0";

    public string DisplayName => "TestingFramework";

    public string Description => "Testing framework sample";

    public Type[] DataTypesProduced => new[] { typeof(TestNodeUpdateMessage), typeof(SessionFileArtifact) };

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            await _logger.LogInformationAsync($"Executing request of type '{context.Request}'");
        }

        switch (context.Request)
        {
            case DiscoverTestExecutionRequest discoverTestExecutionRequest:
                {
                    try
                    {
                        MethodInfo[] tests = GetTestsMethodFromAssemblies();
                        foreach (MethodInfo test in tests)
                        {
                            var testNode = new TestNode()
                            {
                                Uid = $"{test.DeclaringType!.FullName}.{test.Name}",
                                DisplayName = test.Name,
                                Properties = new PropertyBag(DiscoveredTestNodeStateProperty.CachedInstance),
                            };

                            await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(discoverTestExecutionRequest.Session.SessionUid, testNode));
                        }
                    }
                    finally
                    {
                        // Ensure to complete the request also in case of exception
                        context.Complete();
                    }

                    break;
                }

            case RunTestExecutionRequest runTestExecutionRequest:
                {
                    try
                    {
                        await _outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData($"TestingFramework version '{Version}' running tests with parallelism of {_dopValue}") { ForegroundColor = new SystemConsoleColor() { ConsoleColor = ConsoleColor.Green } });

                        StringBuilder reportBody = new();
                        MethodInfo[] tests = GetTestsMethodFromAssemblies();
                        List<Task> results = new();
                        foreach (MethodInfo test in tests)
                        {
                            if (test.GetCustomAttribute<SkipAttribute>() != null)
                            {
                                var skippedTestNode = new TestNode()
                                {
                                    Uid = $"{test.DeclaringType!.FullName}.{test.Name}",
                                    DisplayName = test.Name,
                                    Properties = new PropertyBag(SkippedTestNodeStateProperty.CachedInstance),
                                };

                                await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(runTestExecutionRequest.Session.SessionUid, skippedTestNode));

                                reportBody.AppendLine(CultureInfo.InvariantCulture, $"Test {skippedTestNode.Uid} skipped");

                                continue;
                            }

                            results.Add(Task.Run(async () =>
                            {
                                await _dop.WaitAsync();
                                try
                                {
                                    object? instance = Activator.CreateInstance(test.DeclaringType!);
                                    try
                                    {
                                        test.Invoke(instance, null);

                                        var successfulTestNode = new TestNode()
                                        {
                                            Uid = $"{test.DeclaringType!.FullName}.{test.Name}",
                                            DisplayName = test.Name,
                                            Properties = new PropertyBag(PassedTestNodeStateProperty.CachedInstance),
                                        };

                                        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(runTestExecutionRequest.Session.SessionUid, successfulTestNode));

                                        reportBody.AppendLine(CultureInfo.InvariantCulture, $"Test {successfulTestNode.Uid} succeeded");
                                    }
                                    catch (TargetInvocationException ex) when (ex.InnerException is AssertionException assertionException)
                                    {
                                        var assertionFailedTestNode = new TestNode()
                                        {
                                            Uid = $"{test.DeclaringType!.FullName}.{test.Name}",
                                            DisplayName = test.Name,
                                            Properties = new PropertyBag(new FailedTestNodeStateProperty(assertionException)),
                                        };

                                        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(runTestExecutionRequest.Session.SessionUid, assertionFailedTestNode));

                                        reportBody.AppendLine(CultureInfo.InvariantCulture, $"Test {assertionFailedTestNode.Uid} failed");
                                    }
                                    catch (TargetInvocationException ex)
                                    {
                                        var failedTestNode = new TestNode()
                                        {
                                            Uid = $"{test.DeclaringType!.FullName}.{test.Name}",
                                            DisplayName = test.Name,
                                            Properties = new PropertyBag(new ErrorTestNodeStateProperty(ex.InnerException!)),
                                        };

                                        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(runTestExecutionRequest.Session.SessionUid, failedTestNode));

                                        reportBody.AppendLine(CultureInfo.InvariantCulture, $"Test {failedTestNode.Uid} failed");
                                    }
                                }
                                finally
                                {
                                    _dop.Release();
                                }
                            }));
                        }

                        await Task.WhenAll(results);

                        if (!string.IsNullOrEmpty(_reportFile))
                        {
                            File.WriteAllText(_reportFile, reportBody.ToString());
                            await context.MessageBus.PublishAsync(this, new SessionFileArtifact(runTestExecutionRequest.Session.SessionUid, new FileInfo(_reportFile), "Testing framework report"));
                        }
                    }
                    finally
                    {
                        // Ensure to complete the request also in case of exception
                        context.Complete();
                    }

                    break;
                }

            default:
                throw new NotSupportedException($"Request {context.GetType()} not supported");
        }
    }

    private MethodInfo[] GetTestsMethodFromAssemblies()
        => _assemblies
            .SelectMany(x => x.GetTypes())
            .SelectMany(x => x.GetMethods())
            .Where(x => x.GetCustomAttributes<TestMethodAttribute>().Any())
            .ToArray();

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);
    public void Dispose()
    {
        _dop.Dispose();
    }
}
