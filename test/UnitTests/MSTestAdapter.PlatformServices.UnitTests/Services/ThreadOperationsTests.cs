// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.PlatformServices.Tests.Services;

using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using TestFramework.ForTestingMSTest;

#pragma warning disable SA1649 // SA1649FileNameMustMatchTypeName

public class ThreadOperationsTests : TestContainer
{
    private readonly ThreadOperations _asyncOperations;

    public ThreadOperationsTests()
    {
        _asyncOperations = new ThreadOperations();
    }

    public void ExecuteShouldStartTheActionOnANewThread()
    {
        int actionThreadID = 0;
        void action()
        {
            actionThreadID = Environment.CurrentManagedThreadId;
        }

        CancellationTokenSource tokenSource = new();
        Verify(_asyncOperations.Execute(action, 1000, tokenSource.Token));
        Verify(Environment.CurrentManagedThreadId != actionThreadID);
    }

    public void ExecuteShouldReturnFalseIfTheActionTimesOut()
    {
        static void action() => Task.Delay(100).Wait();

        CancellationTokenSource tokenSource = new();
        Verify(!_asyncOperations.Execute(action, 1, tokenSource.Token));
    }
}
