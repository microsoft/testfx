// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;

using Xunit;

namespace MauiApp1;

/// <summary>
/// XUnit capabilities for test framework registration
/// </summary>
public class XunitFrameworkCapabilities : ITestFrameworkCapabilities
{
    public IReadOnlyCollection<ITestFrameworkCapability> Capabilities => Array.Empty<ITestFrameworkCapability>();
}

/// <summary>
/// XUnit test framework implementation for Microsoft Testing Platform
/// </summary>
public class XunitFramework : Microsoft.Testing.Platform.Extensions.TestFramework.ITestFramework, IDataProducer
{
    private readonly ITestFrameworkCapabilities _capabilities;
    private readonly IServiceProvider _serviceProvider;

    public XunitFramework(ITestFrameworkCapabilities capabilities, IServiceProvider serviceProvider)
    {
        _capabilities = capabilities;
        _serviceProvider = serviceProvider;
    }

    public string Uid => "Xunit.TestFramework";
    public string Version => GetType().Assembly.GetName().Version?.ToString() ?? "1.0.0";
    public string DisplayName => "XUnit Test Framework";
    public string Description => "XUnit Test Framework for .NET MAUI";
    public Type[] DataTypesProduced => new[] { typeof(TestNodeUpdateMessage) };

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
    {
        return Task.FromResult(new CloseTestSessionResult { IsSuccess = true });
    }

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
    {
        return Task.FromResult(new CreateTestSessionResult { IsSuccess = true });
    }

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        // Get the test assemblies
        var testAssembly = Assembly.GetExecutingAssembly();

        // Find tests with FactAttribute
        var testClasses = testAssembly.GetTypes().Where(t => t.GetMethods().Any(m => m.GetCustomAttributes(typeof(FactAttribute), true).Length > 0));

        foreach (var testClass in testClasses)
        {
            var testMethods = testClass.GetMethods()
                            .Where(m => m.GetCustomAttributes(typeof(FactAttribute), true).Length > 0);

            foreach (var testMethod in testMethods)
            {
                string testId = $"{testClass.FullName}.{testMethod.Name}";

                try
                {
                    // Create test instance
                    var testInstance = Activator.CreateInstance(testClass);

                    // Execute the test
                    testMethod.Invoke(testInstance, null);

                    // Report test passed
                    await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(
                                context.Request.Session.SessionUid,
                        new TestNode
                        {
                            Uid = testId,
                            DisplayName = testMethod.Name,
                            Properties = new PropertyBag(PassedTestNodeStateProperty.CachedInstance)
                        }));
                }
                catch (Exception ex)
                {
                    // Report test failed
                    await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(
            context.Request.Session.SessionUid,
              new TestNode
              {
                  Uid = testId,
                  DisplayName = testMethod.Name,
                  Properties = new PropertyBag(new FailedTestNodeStateProperty(ex.ToString()))
              }));
                }
            }
        }

        // Mark as complete after tests have executed
        context.Complete();
    }

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);
}
