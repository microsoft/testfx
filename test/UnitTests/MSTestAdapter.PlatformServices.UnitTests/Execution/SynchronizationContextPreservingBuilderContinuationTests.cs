// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Execution;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Execution;

public sealed class SynchronizationContextPreservingBuilderContinuationTests : TestContainer
{
    public async Task NotifyOnlyAwaiterResumesBothBuildersExactlyOnce()
    {
        List<string> events = [];
        await ExecuteVoidAsync();
        int value = await ExecuteValueAsync();

        value.Should().Be(17);
        events.Should().Equal("void-register", "void-body", "value-register", "value-body");

        async SynchronizationContextPreservingTask ExecuteVoidAsync()
        {
            await new NotifyOnlyAwaitable(() => events.Add("void-register"));
            events.Add("void-body");
        }

        async SynchronizationContextPreservingTask<int> ExecuteValueAsync()
        {
            await new NotifyOnlyAwaitable(() => events.Add("value-register"));
            events.Add("value-body");
            return 17;
        }
    }

    private readonly struct NotifyOnlyAwaitable(Action register)
    {
        public NotifyOnlyAwaiter GetAwaiter() => new(register);
    }

    private readonly struct NotifyOnlyAwaiter(Action register) : INotifyCompletion
    {
        public bool IsCompleted => false;

        public void GetResult()
        {
        }

        public void OnCompleted(Action continuation)
        {
            register();
            continuation();
        }
    }
}
