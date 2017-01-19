// Copyright (c) Microsoft. All rights reserved.

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
            Action action = () => { actionThreadID = Thread.CurrentThread.ManagedThreadId; };

            Assert.IsTrue(this.asyncOperations.Execute(action, 1000));

            Assert.AreNotEqual(Thread.CurrentThread.ManagedThreadId, actionThreadID);
        }

        [TestMethod]
        public void ExecuteShouldKillTheThreadExecutingAsyncOnTimeout()
        {
            ManualResetEvent timeoutMutex = new ManualResetEvent(false);
            var hasReachedEnd = false;
            var isThreadAbortThrown = false;

            Action action = () =>
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
                };

            Assert.IsFalse(this.asyncOperations.Execute(action, 1));
            timeoutMutex.Set();
            
            Assert.IsFalse(hasReachedEnd);
            Assert.IsTrue(isThreadAbortThrown);
        }

        [TestMethod]
        public void ExecuteShouldSpwanOfAthreadWithSpecificAttributes()
        {
            var name = string.Empty;
            var apartmentState = ApartmentState.Unknown;
            var isBackground = false;

            Action action = () =>
                {
                    name = Thread.CurrentThread.Name;
                    apartmentState = Thread.CurrentThread.GetApartmentState();
                    isBackground = Thread.CurrentThread.IsBackground;
                };
            
            Assert.IsTrue(this.asyncOperations.Execute(action, 100));
            
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
    }
}
