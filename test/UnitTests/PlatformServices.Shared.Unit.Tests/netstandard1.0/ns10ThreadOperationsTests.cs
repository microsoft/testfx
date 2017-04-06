// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.PlatformServices.Tests.Services
{
#if !NETCORETESTPROJECT
    extern alias FrameworkV1;

    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
#endif

    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

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
            Action action = () => { actionThreadID = Thread.CurrentThread.ManagedThreadId; };

            Assert.IsTrue(this.asyncOperations.Execute(action, 1000));

            Assert.AreNotEqual(Thread.CurrentThread.ManagedThreadId, actionThreadID);
        }

        [TestMethod]
        public void ExecuteShouldReturnFalseIftheActionTimesout()
        {
            Action action = () => { Task.Delay(100).Wait(); };

            Assert.IsFalse(this.asyncOperations.Execute(action, 1));
        }

        [TestMethod]
        public void ExecuteWithAbortSafetyShouldInvokeTheAction()
        {
            var isInvoked = false;
            Action action = () => { isInvoked = true; };

            this.asyncOperations.ExecuteWithAbortSafety(action);

            Assert.IsTrue(isInvoked);
        }
    }
}
