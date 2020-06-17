// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.PlatformServices.Desktop.UnitTests.Services
{
    extern alias FrameworkV1;

    using System;
    using System.Reflection;
    using System.Threading;

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
    using MSTestAdapter.TestUtilities;

    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;

    [TestClass]
    public class DesktopThreadOperationsTests
    {
        private ThreadOperations asyncOperations;

        public DesktopThreadOperationsTests()
        {
            this.asyncOperations = new ThreadOperations();
        }

        [TestMethod]
        public void ExecuteShouldRunActionOnANewThread()
        {
            int actionThreadID = 0;
            var cancellationTokenSource = new CancellationTokenSource();
            void action()
            {
                actionThreadID = Thread.CurrentThread.ManagedThreadId;
            }

            Assert.IsTrue(this.asyncOperations.Execute(action, 1000, cancellationTokenSource.Token));
            Assert.AreNotEqual(Thread.CurrentThread.ManagedThreadId, actionThreadID);
        }

        [TestMethod]
        public void ExecuteShouldKillTheThreadExecutingAsyncOnTimeout()
        {
            ManualResetEvent timeoutMutex = new ManualResetEvent(false);
            ManualResetEvent actionCompleted = new ManualResetEvent(false);
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

            Assert.IsFalse(this.asyncOperations.Execute(action, 1, cancellationTokenSource.Token));
            timeoutMutex.Set();
            actionCompleted.WaitOne();

            Assert.IsFalse(hasReachedEnd, "Execution Completed successfully");
            Assert.IsTrue(isThreadAbortThrown, "ThreadAbortException not thrown");
        }

        [TestMethod]
        public void ExecuteShouldSpwanOfAthreadWithSpecificAttributes()
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

            Assert.IsTrue(this.asyncOperations.Execute(action, 100, cancellationTokenSource.Token));

            Assert.AreEqual("MSTestAdapter Thread", name);
            Assert.AreEqual(Thread.CurrentThread.GetApartmentState(), apartmentState);
            Assert.IsTrue(isBackground);
        }

        [TestMethod]
        public void ExecuteWithAbortSafetyShouldCatchThreadAbortExceptionsAndResetAbort()
        {
            Action action = () => Thread.CurrentThread.Abort();

            var exception = ActionUtility.PerformActionAndReturnException(() => this.asyncOperations.ExecuteWithAbortSafety(action));

            Assert.IsNotNull(exception);
            Assert.AreEqual(typeof(TargetInvocationException), exception.GetType());
            Assert.AreEqual(typeof(ThreadAbortException), exception.InnerException.GetType());
        }

        [TestMethod]
        public void TokenCancelShouldAbortExecutingAction()
        {
            // setup
            var cancellationTokenSource = new CancellationTokenSource();

            // act
            cancellationTokenSource.CancelAfter(100);
            var result = this.asyncOperations.Execute(() => { Thread.Sleep(10000); }, 100000, cancellationTokenSource.Token);

            // validate
            Assert.IsFalse(result, "The execution failed to abort");
        }

        [TestMethod]
        public void TokenCancelShouldAbortIfAlreadycanceled()
        {
            // setup
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            // act
            var result = this.asyncOperations.Execute(() => { Thread.Sleep(10000); }, 100000, cancellationTokenSource.Token);

            // validate
            Assert.IsFalse(result, "The execution failed to abort");
        }
    }
}
