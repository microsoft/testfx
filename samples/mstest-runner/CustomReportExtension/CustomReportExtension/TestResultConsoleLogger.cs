// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.TestHost;

internal sealed class TestResultConsoleLogger :
    // This is the extension point to subscribe to data messages published to the platform.
    // The type should then be registered as a data consumer in the test host.
    IDataConsumer,
    // This is the extension point to subscribe to test session lifetime events.
    // The type should then be registered as a test session lifetime handler in the test host.
    ITestSessionLifetimeHandler
{
    public string Uid => nameof(TestResultConsoleLogger);

    public string Version => "1.0.0";

    public string DisplayName => "Test results console logger";

    public string Description => "Displays to the console the results of tests execution";

    public Type[] DataTypesConsumed { get; } = new[]
    {
        // Subscribe to test node update messages.
        typeof(TestNodeUpdateMessage)
    };

    public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        var testNodeUpdate = (TestNodeUpdateMessage)value;
        string? resultMessage = testNodeUpdate.TestNode.Properties.Single<TestNodeStateProperty>() switch
        {
            PassedTestNodeStateProperty => "passed",
            FailedTestNodeStateProperty => "failed",
            SkippedTestNodeStateProperty => "was skipped",
            ErrorTestNodeStateProperty => "errored",
            TimeoutTestNodeStateProperty => "timed out",
            CancelledTestNodeStateProperty => "was cancelled",
            _ => null,
        };

        if (resultMessage is not null)
        {
            PrintMessage($"{testNodeUpdate.TestNode.DisplayName} {resultMessage}");
        }

        return Task.CompletedTask;
    }

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task OnTestSessionFinishingAsync(SessionUid sessionUid, CancellationToken cancellationToken)
    {
        PrintMessage($"Closing test session '{sessionUid.Value}'");
        return Task.CompletedTask;
    }

    public Task OnTestSessionStartingAsync(SessionUid sessionUid, CancellationToken cancellationToken)
    {
        PrintMessage($"Starting test session '{sessionUid.Value}'");
        return Task.CompletedTask;
    }

    private static void PrintMessage(string message)
    {
        try
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            // The console service is not yet public as it needs to be finalized so for now we can use Console.WriteLine.
            Console.WriteLine(message);
        }
        finally
        {
            Console.ResetColor();
        }
    }
}
