// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Requests;

namespace TestingPlatformExplorer.TestingFramework;

internal sealed class TestingFramework : ITestFramework, IDataProducer
{
    private readonly TestingFrameworkCapabilities _capabilities;
    private readonly IServiceProvider _serviceProvider;
    private readonly Assembly[] _assemblies;

    public TestingFramework(
        ITestFrameworkCapabilities capabilities,
        IServiceProvider serviceProvider,
        Assembly[] assemblies)
    {
        _capabilities = (TestingFrameworkCapabilities)capabilities;
        _serviceProvider = serviceProvider;
        _assemblies = assemblies;
    }

    public string Uid => nameof(TestingFramework);

    public string Version => "1.0.0";

    public string DisplayName => "TestingFramework";

    public string Description => "Testing framework sample";

    public Type[] DataTypesProduced => new[] { typeof(TestNodeUpdateMessage) };

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
    {
        return Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });
    }

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
    {
        return Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });
    }

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        switch (context.Request)
        {
            case DiscoverTestExecutionRequest discoverTestExecutionRequest:
                {
                    try
                    {
                        var tests = GetTestsMethodFromAssemblies();
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
                        var tests = GetTestsMethodFromAssemblies();
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

                                continue;
                            }

                            results.Add(Task.Run(async () =>
                            {
                                var instance = Activator.CreateInstance(test.DeclaringType!);
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
                                }
                            }));
                        }

                        await Task.WhenAll(results);
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

    public Task<bool> IsEnabledAsync()
    {
        return Task.FromResult(true);
    }
}
