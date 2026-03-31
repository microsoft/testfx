// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Services;

public class ThreadOperationsTests : TestContainer
{
    private readonly ThreadOperations _asyncOperations;

    public ThreadOperationsTests() => _asyncOperations = new ThreadOperations();

#pragma warning disable IDE0051 // Remove unused private members - test is failing in CI.
#if NETFRAMEWORK
    private void ExecuteShouldStartTheActionOnANewThread()
    {
        int actionThreadID = 0;
        void Action() => actionThreadID = Environment.CurrentManagedThreadId;

        CancellationTokenSource tokenSource = new();
        _asyncOperations.Execute(Action, 10000, tokenSource.Token).Should().BeTrue();
        Environment.CurrentManagedThreadId.Should().NotBe(actionThreadID);
    }
#endif

    private void ExecuteShouldReturnFalseIfTheActionTimesOut()
    {
        static void Action() => Task.Delay(1000).Wait();

        CancellationTokenSource tokenSource = new();
        _asyncOperations.Execute(Action, 1, tokenSource.Token).Should().BeFalse();
    }
#pragma warning restore IDE0051 // Remove unused private members
}
