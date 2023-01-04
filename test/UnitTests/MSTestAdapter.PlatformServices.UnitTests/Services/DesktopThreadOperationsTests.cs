// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using TestFramework.ForTestingMSTest;

#if NET462
namespace MSTestAdapter.PlatformServices.UnitTests.Services;
public class DesktopThreadOperationsTests : TestContainer
{
    private readonly ThreadOperations _asyncOperations;

    public DesktopThreadOperationsTests()
    {
        _asyncOperations = new ThreadOperations();
    }

    public async Task ExecuteShouldRunActionOnANewThread()
    {
        int actionThreadID = 0;
        var cancellationTokenSource = new CancellationTokenSource();
        Task Action()
        {
            actionThreadID = Environment.CurrentManagedThreadId;
            return Task.CompletedTask;
        }

        Verify(await _asyncOperations.Execute(Action, 1000, cancellationTokenSource.Token));
        Verify(Environment.CurrentManagedThreadId != actionThreadID);
    }

    public async Task TokenCancelShouldAbortExecutingAction()
    {
        // setup
        var cancellationTokenSource = new CancellationTokenSource();

        // act
        cancellationTokenSource.CancelAfter(100);
        var result = await _asyncOperations.Execute(() => { Thread.Sleep(10000); }, 100000, cancellationTokenSource.Token);

        // validate
        Verify(!result, "The execution failed to abort");
    }

    public async Task TokenCancelShouldAbortIfAlreadyCanceled()
    {
        // setup
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // act
        var result = await _asyncOperations.Execute(() => { Thread.Sleep(10000); }, 100000, cancellationTokenSource.Token);

        // validate
        Verify(!result, "The execution failed to abort");
    }
}
#endif
