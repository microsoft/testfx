// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution
{
    extern alias FrameworkV1;

    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;

    using System;
    using System.Reflection;
    using System.Threading.Tasks;

    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;

    /// <summary>
    /// Tests for <see cref="MethodInfoExtensions"/>
    /// </summary>
    [TestClass]
    public class MethodInfoExtensionsTests
    {
        private readonly DummyTestClass dummyTestClass;

        private readonly MethodInfo dummyMethod;

        private readonly MethodInfo dummyAsyncMethod;

        public MethodInfoExtensionsTests()
        {
            this.dummyTestClass = new DummyTestClass();
            this.dummyMethod = typeof(DummyTestClass).GetMethod("DummyMethod");
            this.dummyAsyncMethod = typeof(DummyTestClass).GetMethod("DummyAsyncMethod");
        }

        [TestMethod]
        public void MethodInfoInvokeAsSynchronousTaskWaitsForCompletionOfAMethodWhichReturnsTask()
        {
            var testMethodCalled = false;
            DummyTestClass.DummyAsyncMethodBody = (x, y) => Task.Run(
                () =>
                    {
                        Assert.AreEqual(10, x);
                        Assert.AreEqual(20, y);
                        testMethodCalled = true;
                    });

            this.dummyAsyncMethod.InvokeAsSynchronousTask(this.dummyTestClass, 10, 20);

            Assert.IsTrue(testMethodCalled);
        }

        [TestMethod]
        public void MethodInfoInvokeAsSynchronousTaskExecutesAMethodWhichDoesNotReturnATask()
        {
            var testMethodCalled = false;
            DummyTestClass.DummyMethodBody = (x, y) =>
                {
                    Assert.AreEqual(10, x);
                    Assert.AreEqual(20, y);
                    testMethodCalled = true;
                    return true;
                };

            this.dummyMethod.InvokeAsSynchronousTask(this.dummyTestClass, 10, 20);

            Assert.IsTrue(testMethodCalled);
        }


        public class DummyTestClass
        {
            public static Func<int, int, Task> DummyAsyncMethodBody { get; set; }

            public static Func<int, int, bool> DummyMethodBody { get; set; }

            public bool DummyMethod(int x, int y)
            {
                return DummyMethodBody(x, y);
            }

            public async Task DummyAsyncMethod(int x, int y)
            {
                await DummyAsyncMethodBody(x, y);
            }
        }
    }
}
