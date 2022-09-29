// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET462
namespace MSTestAdapter.PlatformServices.UnitTests.Services;

using System;
using System.Reflection;
using System.Threading;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using TestFramework.ForTestingMSTest;

public class DesktopThreadOperationsTests : TestContainer
{
    private readonly ThreadOperations _asyncOperations;

    public DesktopThreadOperationsTests()
    {
        _asyncOperations = new ThreadOperations();
    }

    public void ExecuteShouldRunActionOnANewThread()
    {
        int actionThreadID = 0;
        var cancellationTokenSource = new CancellationTokenSource();
        void action()
        {
            actionThreadID = Environment.CurrentManagedThreadId;
        }

        Verify(_asyncOperations.Execute(action, 1000, cancellationTokenSource.Token));
        Verify(Environment.CurrentManagedThreadId != actionThreadID);
    }

    public void ExecuteShouldKillTheThreadExecutingAsyncOnTimeout()
    {
        ManualResetEvent timeoutMutex = new(false);
        ManualResetEvent actionCompleted = new(false);
        var hasReachedEnd = false;
        var isThreadAbortThrown = false;
        var cancellationTokenSource = new CancellationTokenSource();

        void action()
        {
            try
            {
                timeoutMutex.WaitOne();
                hasReachedEnd = true;
            }
            catch (ThreadAbortException)
            {
                isThreadAbortThrown = true;

                // Resetting abort because there is a warning being thrown in the tests pane.
                Thread.ResetAbort();
            }
            finally
            {
                actionCompleted.Set();
            }
        }

        Verify(!_asyncOperations.Execute(action, 1, cancellationTokenSource.Token));
        timeoutMutex.Set();
        actionCompleted.WaitOne();

        Verify(!hasReachedEnd, "Execution Completed successfully");
        Verify(isThreadAbortThrown, "ThreadAbortException not thrown");
    }

    public void ExecuteShouldSpawnThreadWithSpecificAttributes()
    {
        var name = string.Empty;
        var apartmentState = ApartmentState.Unknown;
        var isBackground = false;
        var cancellationTokenSource = new CancellationTokenSource();
        void action()
        {
            name = Thread.CurrentThread.Name;
            apartmentState = Thread.CurrentThread.GetApartmentState();
            isBackground = Thread.CurrentThread.IsBackground;
        }

        Verify(_asyncOperations.Execute(action, 100, cancellationTokenSource.Token));

        Verify("MSTestAdapter Thread" == name);
        Verify(Thread.CurrentThread.GetApartmentState() == apartmentState);
        Verify(isBackground);
    }

    public void ExecuteWithAbortSafetyShouldCatchThreadAbortExceptionsAndResetAbort()
    {
        static void action() => Thread.CurrentThread.Abort();

        var exception = VerifyThrows(() => _asyncOperations.ExecuteWithAbortSafety(action));

        Verify(exception is not null);
        Verify(typeof(TargetInvocationException) == exception.GetType());
        Verify(typeof(ThreadAbortException) == exception.InnerException.GetType());
    }

    public void TokenCancelShouldAbortExecutingAction()
    {
        // setup
        var cancellationTokenSource = new CancellationTokenSource();

        // act
        cancellationTokenSource.CancelAfter(100);
        var result = _asyncOperations.Execute(() => { Thread.Sleep(10000); }, 100000, cancellationTokenSource.Token);

        // validate
        Verify(!result, "The execution failed to abort");
    }

    public void TokenCancelShouldAbortIfAlreadyCanceled()
    {
        // setup
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // act
        var result = _asyncOperations.Execute(() => { Thread.Sleep(10000); }, 100000, cancellationTokenSource.Token);

        // validate
        Verify(!result, "The execution failed to abort");
    }
}
#endif
