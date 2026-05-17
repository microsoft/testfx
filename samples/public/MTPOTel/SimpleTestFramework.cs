// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.TestHost;

namespace MTPOTel;

internal sealed class SimpleTestFramework : ITestFramework, IDataProducer
{
    private readonly IServiceProvider _serviceProvider;

    public SimpleTestFramework(IServiceProvider serviceProvider)
        => _serviceProvider = serviceProvider;

    public string Uid => nameof(SimpleTestFramework);

    public string Version => "1.0.0";

    public string DisplayName => "Simple Test Framework";

    public string Description => "A simple test framework that returns 5 tests run sequentially";

    public Type[] DataTypesProduced => [typeof(TestNodeUpdateMessage)];

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult { IsSuccess = true });

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        var sessionUid = new SessionUid("SimpleTestSession");

        // Create 5 tests to run sequentially
        for (int i = 1; i <= 5; i++)
        {
            string testId = $"Test{i}";
            string testName = $"Simple Test {i}";

            // Publish test node as in-progress
            await context.MessageBus.PublishAsync(
                this,
                new TestNodeUpdateMessage(
                    sessionUid,
                    new TestNode
                    {
                        Uid = testId,
                        DisplayName = testName,
                        Properties = new PropertyBag(new InProgressTestNodeStateProperty()),
                    }));

            // Simulate test execution
            Console.WriteLine($"Executing {testName}...");
            await Task.Delay(500); // Simulate some work

            // Determine test result (all pass for now)
            bool testPassed = true;

            // Publish test node as passed or failed
            IProperty resultProperty = testPassed
                ? new PassedTestNodeStateProperty()
                : new FailedTestNodeStateProperty(new InvalidOperationException($"{testName} failed"));

            await context.MessageBus.PublishAsync(
                this,
                new TestNodeUpdateMessage(
                    sessionUid,
                    new TestNode
                    {
                        Uid = testId,
                        DisplayName = testName,
                        Properties = new PropertyBag(resultProperty),
                    }));

            Console.WriteLine($"{testName} completed: {(testPassed ? "Passed" : "Failed")}");
        }

        context.Complete();
    }
}
