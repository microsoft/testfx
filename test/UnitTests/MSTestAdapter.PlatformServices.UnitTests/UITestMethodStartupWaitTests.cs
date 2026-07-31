// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if WIN_UI
using AwesomeAssertions;

using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using Microsoft.VisualStudio.TestTools.UnitTesting.Resources;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests;

/// <summary>
/// Covers the guards that keep a WinUI startup problem a test failure rather than the hung run described
/// in issue #2784. Neither failure is reproducible on demand from a test host - one needs XAML to tear
/// down the UI thread natively, the other needs startup to stall for minutes - so the decision is
/// exercised directly instead of through a real application.
/// </summary>
public class UITestMethodStartupWaitTests : TestContainer
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(1);

    // A generous timeout, so that reaching it would mean the liveness guard failed to fire rather than
    // the test racing the clock.
    private static readonly TimeSpan UnreachableTimeout = TimeSpan.FromMinutes(5);

    public void WaitForApplicationStartup_WhenUIThreadExitsWithoutCompleting_ReportsThatTheApplicationFailedToStart()
    {
        TaskCompletionSource<object> startupCompletion = new();

        UITestMethodAttribute.WaitForApplicationStartup(startupCompletion, _ => true, UnreachableTimeout, PollInterval);

        startupCompletion.Task.IsFaulted.Should().BeTrue();
        startupCompletion.Task.Exception!.InnerException.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Be(FrameworkExtensionsMessages.WinUIApplicationFailedToStart);
    }

    public void WaitForApplicationStartup_WhenStartupStallsOnALiveThread_TimesOut()
    {
        TaskCompletionSource<object> startupCompletion = new();

        // The thread never exits and the callback never completes, which is what a stalled startup looks
        // like: only the timeout can end this wait.
        UITestMethodAttribute.WaitForApplicationStartup(startupCompletion, _ => false, TimeSpan.Zero, PollInterval);

        startupCompletion.Task.IsFaulted.Should().BeTrue();
        startupCompletion.Task.Exception!.InnerException.Should().BeOfType<TimeoutException>()
            .Which.Message.Should().Be(string.Format(
                CultureInfo.CurrentCulture, FrameworkExtensionsMessages.WinUIApplicationStartupTimedOut, TimeSpan.Zero));
    }

    public void WaitForApplicationStartup_WhenTheCallbackCompletesWhileWaiting_LeavesTheResultAlone()
    {
        TaskCompletionSource<object> startupCompletion = new();
        object expectedResult = new();
        int polls = 0;

        // The application comes up on the third poll, as it does on the success path where the callback
        // completes the wait while the UI thread keeps pumping.
        bool WaitForUIThreadExit(TimeSpan _)
        {
            if (++polls == 3)
            {
                startupCompletion.SetResult(expectedResult);
            }

            return false;
        }

        UITestMethodAttribute.WaitForApplicationStartup(startupCompletion, WaitForUIThreadExit, UnreachableTimeout, PollInterval);

        startupCompletion.Task.IsCompletedSuccessfully.Should().BeTrue();
        startupCompletion.Task.Result.Should().BeSameAs(expectedResult);
    }

    public void WaitForApplicationStartup_WhenStartupAlreadyCompleted_DoesNotWait()
    {
        TaskCompletionSource<object> startupCompletion = new();
        object expectedResult = new();
        startupCompletion.SetResult(expectedResult);

        // A poll would mean the wait ran despite startup being done, and an already-completed wait must
        // not be turned into a failure.
        UITestMethodAttribute.WaitForApplicationStartup(
            startupCompletion,
            _ => throw new InvalidOperationException("The UI thread must not be polled once startup completed."),
            TimeSpan.Zero,
            PollInterval);

        startupCompletion.Task.Result.Should().BeSameAs(expectedResult);
    }
}
#endif
