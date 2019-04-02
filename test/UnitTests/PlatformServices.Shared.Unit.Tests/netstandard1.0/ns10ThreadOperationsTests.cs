// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.PlatformServices.Tests.Services
{
#if NETCOREAPP1_1
    using Microsoft.VisualStudio.TestTools.UnitTesting;
#else
    extern alias FrameworkV1;

    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
#endif

    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

#pragma warning disable SA1649 // SA1649FileNameMustMatchTypeName

    [TestClass]
    public class ThreadOperationsTests
    {
        private ThreadOperations asyncOperations;

        public ThreadOperationsTests()
        {
            this.asyncOperations = new ThreadOperations();
        }

        [TestMethod]
        public void ExecuteShouldStartTheActionOnANewThread()
        {
            int actionThreadID = 0;
            void action()
            {
                actionThreadID = Thread.CurrentThread.ManagedThreadId;
            }

            CancellationTokenSource tokenSource = new CancellationTokenSource();
            Assert.IsTrue(this.asyncOperations.Execute(action, 1000, tokenSource.Token));
            Assert.AreNotEqual(Thread.CurrentThread.ManagedThreadId, actionThreadID);
        }

        [TestMethod]
        public void ExecuteShouldReturnFalseIftheActionTimesout()
        {
            void action()
            {
                Task.Delay(100).Wait();
            }

            CancellationTokenSource tokenSource = new CancellationTokenSource();
            Assert.IsFalse(this.asyncOperations.Execute(action, 1, tokenSource.Token));
        }

        [TestMethod]
        public void ExecuteWithAbortSafetyShouldInvokeTheAction()
        {
            var isInvoked = false;
            void action()
            {
                isInvoked = true;
            }

            this.asyncOperations.ExecuteWithAbortSafety(action);

            Assert.IsTrue(isInvoked);
        }
    }
}
