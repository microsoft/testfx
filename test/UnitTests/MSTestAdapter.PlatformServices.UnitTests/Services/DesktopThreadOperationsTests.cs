// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET462

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Services;

public class DesktopThreadOperationsTests : TestContainer
{
    private readonly ThreadOperations _asyncOperations;

    public DesktopThreadOperationsTests() => _asyncOperations = new ThreadOperations();

    public void ExecuteShouldRunActionOnANewThread()
    {
        int actionThreadID = 0;
        var cancellationTokenSource = new CancellationTokenSource();
        void Action() => actionThreadID = Environment.CurrentManagedThreadId;

        Verify(_asyncOperations.Execute(Action, 1000, cancellationTokenSource.Token));
        Verify(Environment.CurrentManagedThreadId != actionThreadID);
    }

    public void TokenCancelShouldAbortExecutingAction()
    {
        // setup
        var cancellationTokenSource = new CancellationTokenSource();

        // act
        cancellationTokenSource.CancelAfter(100);
        bool result = _asyncOperations.Execute(() => Thread.Sleep(10000), 100000, cancellationTokenSource.Token);

        // validate
        Verify(!result, "The execution failed to abort");
    }

    public void TokenCancelShouldAbortIfAlreadyCanceled()
    {
        // setup
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // act
        bool result = _asyncOperations.Execute(() => Thread.Sleep(10000), 100000, cancellationTokenSource.Token);

        // validate
        Verify(!result, "The execution failed to abort");
    }
}
#endif
