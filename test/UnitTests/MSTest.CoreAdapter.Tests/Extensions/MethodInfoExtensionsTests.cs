// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Extensions
{
    extern alias FrameworkV1;
    extern alias FrameworkV2;
    extern alias FrameworkV2CoreExtension;

    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
    using StringAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.StringAssert;

    using System;
    using System.Reflection;
    using System.Threading.Tasks;

    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
    
    using UTF = FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting;
    using UTFExtension = FrameworkV2CoreExtension::Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class MethodInfoExtensionsTests
    {
        #region HasCorrectClassOrAssemblyInitializeSignature tests

        [TestMethod]
        public void HasCorrectClassOrAssemblyInitializeSignatureShouldReturnFalseForNonStaticMethods()
        {
            var methodInfo = typeof(DummyTestClass).GetMethod("PublicMethod");
            Assert.IsFalse(methodInfo.HasCorrectClassOrAssemblyInitializeSignature());
        }

        [TestMethod]
        public void HasCorrectClassOrAssemblyInitializeSignatureShouldReturnFalseForNonPublicMethods()
        {
            var methodInfo = typeof(DummyTestClass).GetMethod("InternalStaticMethod", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            Assert.IsFalse(methodInfo.HasCorrectClassOrAssemblyInitializeSignature());
        }

        [TestMethod]
        public void HasCorrectClassOrAssemblyInitializeSignatureShouldReturnFalseForMethodsNotHavingOneParameter()
        {
            var methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticMethod");
            Assert.IsFalse(methodInfo.HasCorrectClassOrAssemblyInitializeSignature());
        }

        [TestMethod]
        public void HasCorrectClassOrAssemblyInitializeSignatureShouldReturnFalseForMethodsNotTestContextParameter()
        {
            var methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticMethodWithInt");
            Assert.IsFalse(methodInfo.HasCorrectClassOrAssemblyInitializeSignature());
        }

        [TestMethod]
        public void HasCorrectClassOrAssemblyInitializeSignatureShouldReturnFalseForMethodsNotHavingVoidOrAsyncReturnType()
        {
            var methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticMethodWithTCReturningInt");
            Assert.IsFalse(methodInfo.HasCorrectClassOrAssemblyInitializeSignature());
        }

        [TestMethod]
        public void HasCorrectClassOrAssemblyInitializeSignatureShouldReturnTrueForTestMethods()
        {
            var methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticMethodWithTC");
            Assert.IsTrue(methodInfo.HasCorrectClassOrAssemblyInitializeSignature());
        }

        [TestMethod]
        public void HasCorrectClassOrAssemblyInitializeSignatureShouldReturnTrueForAsyncTestMethods()
        {
            var methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticAsyncTaskMethodWithTC");
            Assert.IsTrue(methodInfo.HasCorrectClassOrAssemblyInitializeSignature());
        }

        [TestMethod]
        public void HasCorrectClassOrAssemblyInitializeSignatureShouldReturnFalseForAsyncTestMethodsWithNonTaskReturnTypes()
        {
            var methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticAsyncVoidMethodWithTC");
            Assert.IsFalse(methodInfo.HasCorrectClassOrAssemblyInitializeSignature());
        }

        #endregion

        #region HasCorrectClassOrAssemblyCleanupSignature tests

        [TestMethod]
        public void HasCorrectClassOrAssemblyCleanupSignatureShouldReturnFalseForNonStaticMethods()
        {
            var methodInfo = typeof(DummyTestClass).GetMethod("PublicMethod");
            Assert.IsFalse(methodInfo.HasCorrectClassOrAssemblyCleanupSignature());
        }

        [TestMethod]
        public void HasCorrectClassOrAssemblyCleanupSignatureShouldReturnFalseForNonPublicMethods()
        {
            var methodInfo = typeof(DummyTestClass).GetMethod("InternalStaticMethod", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            Assert.IsFalse(methodInfo.HasCorrectClassOrAssemblyCleanupSignature());
        }

        [TestMethod]
        public void HasCorrectClassOrAssemblyCleanupSignatureShouldReturnFalseForMethodsHavingParameters()
        {
            var methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticMethodWithInt");
            Assert.IsFalse(methodInfo.HasCorrectClassOrAssemblyCleanupSignature());
        }

        [TestMethod]
        public void HasCorrectClassOrAssemblyCleanupSignatureShouldReturnFalseForMethodsNotHavingVoidOrAsyncReturnType()
        {
            var methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticMethodReturningInt");
            Assert.IsFalse(methodInfo.HasCorrectClassOrAssemblyCleanupSignature());
        }

        [TestMethod]
        public void HasCorrectClassOrAssemblyCleanupSignatureShouldReturnTrueForTestMethods()
        {
            var methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticMethod");
            Assert.IsTrue(methodInfo.HasCorrectClassOrAssemblyCleanupSignature());
        }

        [TestMethod]
        public void HasCorrectClassOrAssemblyCleanupSignatureShouldReturnTrueForAsyncTestMethods()
        {
            var methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticAsyncTaskMethod");
            Assert.IsTrue(methodInfo.HasCorrectClassOrAssemblyCleanupSignature());
        }

        [TestMethod]
        public void HasCorrectClassOrAssemblyCleanupSignatureShouldReturnFalseForAsyncTestMethodsWithNonTaskReturnTypes()
        {
            var methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticAsyncVoidMethod");
            Assert.IsFalse(methodInfo.HasCorrectClassOrAssemblyCleanupSignature());
        }

        #endregion

        #region HasCorrectTestInitializeOrCleanupSignature tests

        [TestMethod]
        public void HasCorrectTestInitializeOrCleanupSignatureShouldReturnFalseForStaticMethods()
        {
            var methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticMethod");
            Assert.IsFalse(methodInfo.HasCorrectTestInitializeOrCleanupSignature());
        }

        [TestMethod]
        public void HasCorrectTestInitializeOrCleanupSignatureShouldReturnFalseForNonPublicMethods()
        {
            var methodInfo = typeof(DummyTestClass).GetMethod("InternalMethod", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            Assert.IsFalse(methodInfo.HasCorrectTestInitializeOrCleanupSignature());
        }

        [TestMethod]
        public void HasCorrectTestInitializeOrCleanupSignatureShouldReturnFalseForMethodsHavingParameters()
        {
            var methodInfo = typeof(DummyTestClass).GetMethod("PublicMethodWithInt");
            Assert.IsFalse(methodInfo.HasCorrectTestInitializeOrCleanupSignature());
        }

        [TestMethod]
        public void HasCorrectTestInitializeOrCleanupSignatureShouldReturnFalseForMethodsNotHavingVoidOrAsyncReturnType()
        {
            var methodInfo = typeof(DummyTestClass).GetMethod("PublicMethodReturningInt");
            Assert.IsFalse(methodInfo.HasCorrectTestInitializeOrCleanupSignature());
        }

        [TestMethod]
        public void HasCorrectTestInitializeOrCleanupSignatureShouldReturnTrueForTestMethods()
        {
            var methodInfo = typeof(DummyTestClass).GetMethod("PublicMethod");
            Assert.IsTrue(methodInfo.HasCorrectTestInitializeOrCleanupSignature());
        }

        [TestMethod]
        public void HasCorrectTestInitializeOrCleanupSignatureShouldReturnTrueForAsyncTestMethods()
        {
            var methodInfo = typeof(DummyTestClass).GetMethod("PublicAsyncTaskMethod");
            Assert.IsTrue(methodInfo.HasCorrectTestInitializeOrCleanupSignature());
        }

        [TestMethod]
        public void HasCorrectTestInitializeOrCleanupSignatureShouldReturnFalseForAsyncTestMethodsWithNonTaskReturnTypes()
        {
            var methodInfo = typeof(DummyTestClass).GetMethod("PublicAsyncVoidMethod");
            Assert.IsFalse(methodInfo.HasCorrectTestInitializeOrCleanupSignature());
        }

        #endregion

        #region HasCorrectTestMethodSignature tests

        [TestMethod]
        public void HasCorrectTestMethodSignatureShouldReturnFalseForAbstractMethods()
        {
            var methodInfo = typeof(DummyTestClass).GetMethod("PublicAbstractMethod");
            Assert.IsFalse(methodInfo.HasCorrectTestMethodSignature(false));
        }

        [TestMethod]
        public void HasCorrectTestMethodSignatureShouldReturnFalseForStaticMethods()
        {
            var methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticMethod");
            Assert.IsFalse(methodInfo.HasCorrectTestMethodSignature(false));
        }

        [TestMethod]
        public void HasCorrectTestMethodSignatureShouldReturnFalseForGenericMethods()
        {
            var methodInfo = typeof(DummyTestClass).GetMethod("PublicGenericMethod");
            Assert.IsFalse(methodInfo.HasCorrectTestMethodSignature(false));
        }

        [TestMethod]
        public void HasCorrectTestMethodSignatureShouldReturnFalseForNonPublicMethods()
        {
            var methodInfo = typeof(DummyTestClass).GetMethod("InternalMethod", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            Assert.IsFalse(methodInfo.HasCorrectTestMethodSignature(false));
        }

        [TestMethod]
        public void HasCorrectTestMethodSignatureShouldReturnFalseForMethodsHavingParamters()
        {
            var methodInfo = typeof(DummyTestClass).GetMethod("PublicMethodWithInt");
            Assert.IsFalse(methodInfo.HasCorrectTestMethodSignature(false));
        }

        [TestMethod]
        public void HasCorrectTestMethodSignatureShouldReturnTrueForMethodsWithParametersWhenParamterCountIsIgnored()
        {
            var methodInfo = typeof(DummyTestClass).GetMethod("PublicMethodWithInt");
            Assert.IsTrue(methodInfo.HasCorrectTestMethodSignature(true));
        }

        [TestMethod]
        public void HasCorrectTestMethodSignatureShouldReturnTrueForTestMethods()
        {
            var methodInfo = typeof(DummyTestClass).GetMethod("PublicMethod");
            Assert.IsTrue(methodInfo.HasCorrectTestMethodSignature(false));
        }

        [TestMethod]
        public void HasCorrectTestMethodSignatureShouldReturnTrueForAsyncTestMethods()
        {
            var methodInfo = typeof(DummyTestClass).GetMethod("PublicAsyncTaskMethod");
            Assert.IsTrue(methodInfo.HasCorrectTestMethodSignature(false));
        }

        [TestMethod]
        public void HasCorrectTestMethodSignatureShouldReturnFalseForAsyncTestMethodsWithNonTaskReturnTypes()
        {
            var methodInfo = typeof(DummyTestClass).GetMethod("PublicAsyncVoidMethod");
            Assert.IsFalse(methodInfo.HasCorrectTestMethodSignature(false));
        }

        #endregion

        #region HasCorrectTimeout tests

        [TestMethod]
        public void HasCorrectTimeoutShouldReturnFalseForMethodsWithoutTimeoutAttribute()
        {
            var methodInfo = typeof(DummyTestClass).GetMethod("PublicMethod");
            Assert.IsFalse(methodInfo.HasCorrectTimeout());
        }

        [TestMethod]
        public void HasCorrectTimeoutShouldReturnFalseForMethodsWithInvalidTimeoutAttribute()
        {
            var methodInfo = typeof(DummyTestClass).GetMethod("PublicMethodWithInvalidTimeout");
            Assert.IsFalse(methodInfo.HasCorrectTimeout());
        }

        [TestMethod]
        public void HasCorrectTimeoutShouldReturnTrueForMethodsWithTimeoutAttribute()
        {
            var methodInfo = typeof(DummyTestClass).GetMethod("PublicMethodWithTimeout");
            Assert.IsTrue(methodInfo.HasCorrectTimeout());
        }

        #endregion

        #region IsVoidOrTaskReturnType tests

        [TestMethod]
        public void IsVoidOrTaskReturnTypeShouldReturnTrueForVoidMethods()
        {
            var methodInfo = typeof(DummyTestClass).GetMethod("PublicMethod");
            Assert.IsTrue(methodInfo.IsVoidOrTaskReturnType());
        }

        [TestMethod]
        public void IsVoidOrTaskReturnTypeShouldReturnTrueForAsyncTaskMethods()
        {
            var methodInfo = typeof(DummyTestClass).GetMethod("PublicAsyncTaskMethod");
            Assert.IsTrue(methodInfo.IsVoidOrTaskReturnType());
        }

        [TestMethod]
        public void IsVoidOrTaskReturnTypeShouldReturnFalseForNonVoidMethods()
        {
            var methodInfo = typeof(DummyTestClass).GetMethod("PublicMethodReturningInt");
            Assert.IsFalse(methodInfo.IsVoidOrTaskReturnType());
        }

        [TestMethod]
        public void IsVoidOrTaskReturnTypeShouldReturnTrueForAsyncNonTaskMethods()
        {
            var methodInfo = typeof(DummyTestClass).GetMethod("PublicAsyncVoidMethod");
            Assert.IsFalse(methodInfo.IsVoidOrTaskReturnType());
        }

        #endregion

        #region GetAsyncTypeName tests

        [TestMethod]
        public void GetAsyncTypeNameShouldReturnNullForVoidMethods()
        {
            var methodInfo = typeof(DummyTestClass).GetMethod("PublicMethod");
            Assert.IsNull(methodInfo.GetAsyncTypeName());
        }

        [TestMethod]
        public void GetAsyncTypeNameShouldReturnStateMachineTypeNameForAsyncMethods()
        {
            var methodInfo = typeof(DummyTestClass).GetMethod("PublicAsyncVoidMethod");
            StringAssert.StartsWith(methodInfo.GetAsyncTypeName(), "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Extensions.MethodInfoExtensionsTests+DummyTestClass+<PublicAsyncVoidMethod>");
        }

        #endregion

        #region InvokeAsSynchronousTask tests

        [TestMethod]
        public void MethodInfoInvokeAsSynchronousTaskWaitsForCompletionOfAMethodWhichReturnsTask()
        {
            var testMethodCalled = false;
            DummyTestClass2.DummyAsyncMethodBody = (x, y) => Task.Run(
                () =>
                {
                    Assert.AreEqual(10, x);
                    Assert.AreEqual(20, y);
                    testMethodCalled = true;
                });

            var dummyTestClass = new DummyTestClass2();
            var dummyAsyncMethod = typeof(DummyTestClass2).GetMethod("DummyAsyncMethod");
            
            dummyAsyncMethod.InvokeAsSynchronousTask(dummyTestClass, 10, 20);

            Assert.IsTrue(testMethodCalled);
        }

        [TestMethod]
        public void MethodInfoInvokeAsSynchronousTaskExecutesAMethodWhichDoesNotReturnATask()
        {
            var testMethodCalled = false;
            DummyTestClass2.DummyMethodBody = (x, y) =>
            {
                Assert.AreEqual(10, x);
                Assert.AreEqual(20, y);
                testMethodCalled = true;
                return true;
            };

            var dummyTestClass = new DummyTestClass2();
            var dummyMethod = typeof(DummyTestClass2).GetMethod("DummyMethod");

            dummyMethod.InvokeAsSynchronousTask(dummyTestClass, 10, 20);

            Assert.IsTrue(testMethodCalled);
        }

        #endregion

        #region Dummy Class 

        private abstract class DummyTestClass
        {
            internal void InternalMethod()
            {
            }

            internal static void InternalStaticMethod()
            {
            }

            public void PublicMethod()
            {
            }

            public abstract void PublicAbstractMethod();

            public void PublicGenericMethod<T>(T i)
            {
            }

            public static void PublicStaticMethod()
            {
            }

            public static void PublicStaticMethodWithInt(int a)
            {
            }

            public void PublicMethodWithInt(int a)
            {
            }

            public static int PublicStaticMethodWithTCReturningInt(UTFExtension.TestContext tc)
            {
                return 0;
            }

            public static void PublicStaticMethodWithTC(UTFExtension.TestContext tc)
            {
            }

            public async static Task PublicStaticAsyncTaskMethodWithTC(UTFExtension.TestContext tc)
            {
            }

            public async static void PublicStaticAsyncVoidMethodWithTC(UTFExtension.TestContext tc)
            {
            }

            public static int PublicStaticMethodReturningInt()
            {
                return 0;
            }

            public async static Task PublicStaticAsyncTaskMethod()
            {
            }

            public async static void PublicStaticAsyncVoidMethod()
            {
            }

            public int PublicMethodReturningInt()
            {
                return 0;
            }

            public async Task PublicAsyncTaskMethod()
            {
            }

            public async void PublicAsyncVoidMethod()
            {
            }

            [UTF.Timeout(-11)]
            public void PublicMethodWithInvalidTimeout()
            {
            }

            [UTF.Timeout(11)]
            public void PublicMethodWithTimeout()
            {
            }
        }

        public class DummyTestClass2
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

        #endregion
    }
}
