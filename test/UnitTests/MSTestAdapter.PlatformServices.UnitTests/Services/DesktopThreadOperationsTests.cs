// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK

using AwesomeAssertions;

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

        _asyncOperations.Execute(Action, 1000, cancellationTokenSource.Token).Should().BeTrue();
        Environment.CurrentManagedThreadId.Should().NotBe(actionThreadID);
    }

    public void TokenCancelShouldAbortExecutingAction()
    {
        // setup
        var cancellationTokenSource = new CancellationTokenSource();

        // act
        // Give more time for thread creation and Task.Run setup to ensure cancellation
        // is detected during the Wait() call rather than timing out
        cancellationTokenSource.CancelAfter(500);
        bool result = _asyncOperations.Execute(() => Thread.Sleep(10000), 100000, cancellationTokenSource.Token);

        // validate
        result.Should().BeFalse("The execution failed to abort");
    }

    public void TokenCancelShouldAbortIfAlreadyCanceled()
    {
        // setup
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // act
        bool result = _asyncOperations.Execute(() => Thread.Sleep(10000), 100000, cancellationTokenSource.Token);

        // validate
        result.Should().BeFalse("The execution failed to abort");
    }
}
#endif
