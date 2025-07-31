// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using TestFramework.ForTestingMSTest;

namespace MSTest.PlatformServices.Tests.Services;
#pragma warning disable SA1649 // SA1649FileNameMustMatchTypeName

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
        Verify(_asyncOperations.Execute(Action, 10000, tokenSource.Token));
        Verify(Environment.CurrentManagedThreadId != actionThreadID);
    }
#endif

    private void ExecuteShouldReturnFalseIfTheActionTimesOut()
    {
        static void Action() => Task.Delay(1000).Wait();

        CancellationTokenSource tokenSource = new();
        Verify(!_asyncOperations.Execute(Action, 1, tokenSource.Token));
    }
#pragma warning restore IDE0051 // Remove unused private members
}
