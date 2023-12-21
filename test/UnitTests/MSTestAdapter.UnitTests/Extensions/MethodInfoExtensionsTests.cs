// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

using TestFramework.ForTestingMSTest;

using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;
using UTFExtension = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Extensions;

public class MethodInfoExtensionsTests : TestContainer
{
    #region HasCorrectClassOrAssemblyInitializeSignature tests

    public void HasCorrectClassOrAssemblyInitializeSignatureShouldReturnFalseForNonStaticMethods()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("PublicMethod");
        Verify(!methodInfo.HasCorrectClassOrAssemblyInitializeSignature());
    }

    public void HasCorrectClassOrAssemblyInitializeSignatureShouldReturnFalseForNonPublicMethods()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("InternalStaticMethod", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
        Verify(!methodInfo.HasCorrectClassOrAssemblyInitializeSignature());
    }

    public void HasCorrectClassOrAssemblyInitializeSignatureShouldReturnFalseForMethodsNotHavingOneParameter()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticMethod");
        Verify(!methodInfo.HasCorrectClassOrAssemblyInitializeSignature());
    }

    public void HasCorrectClassOrAssemblyInitializeSignatureShouldReturnFalseForMethodsNotTestContextParameter()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticMethodWithInt");
        Verify(!methodInfo.HasCorrectClassOrAssemblyInitializeSignature());
    }

    public void HasCorrectClassOrAssemblyInitializeSignatureShouldReturnFalseForMethodsNotHavingVoidOrAsyncReturnType()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticMethodWithTCReturningInt");
        Verify(!methodInfo.HasCorrectClassOrAssemblyInitializeSignature());
    }

    public void HasCorrectClassOrAssemblyInitializeSignatureShouldReturnTrueForTestMethods()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticMethodWithTC");
        Verify(methodInfo.HasCorrectClassOrAssemblyInitializeSignature());
    }

    public void HasCorrectClassOrAssemblyInitializeSignatureShouldReturnTrueForAsyncTestMethods()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticAsyncTaskMethodWithTC");
        Verify(methodInfo.HasCorrectClassOrAssemblyInitializeSignature());
    }

    public void HasCorrectClassOrAssemblyInitializeSignatureShouldReturnTrueForTestMethodsWithoutAsync()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticNonAsyncTaskMethodWithTC");
        Verify(methodInfo.HasCorrectClassOrAssemblyInitializeSignature());
    }

    public void HasCorrectClassOrAssemblyInitializeSignatureShouldReturnFalseForAsyncTestMethodsWithNonTaskReturnTypes()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticAsyncVoidMethodWithTC");
        Verify(!methodInfo.HasCorrectClassOrAssemblyInitializeSignature());
    }

    #endregion

    #region HasCorrectClassOrAssemblyCleanupSignature tests

    public void HasCorrectClassOrAssemblyCleanupSignatureShouldReturnFalseForNonStaticMethods()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("PublicMethod");
        Verify(!methodInfo.HasCorrectClassOrAssemblyCleanupSignature());
    }

    public void HasCorrectClassOrAssemblyCleanupSignatureShouldReturnFalseForNonPublicMethods()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("InternalStaticMethod", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
        Verify(!methodInfo.HasCorrectClassOrAssemblyCleanupSignature());
    }

    public void HasCorrectClassOrAssemblyCleanupSignatureShouldReturnFalseForMethodsHavingParameters()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticMethodWithInt");
        Verify(!methodInfo.HasCorrectClassOrAssemblyCleanupSignature());
    }

    public void HasCorrectClassOrAssemblyCleanupSignatureShouldReturnFalseForMethodsNotHavingVoidOrAsyncReturnType()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticMethodReturningInt");
        Verify(!methodInfo.HasCorrectClassOrAssemblyCleanupSignature());
    }

    public void HasCorrectClassOrAssemblyCleanupSignatureShouldReturnTrueForTestMethods()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticMethod");
        Verify(methodInfo.HasCorrectClassOrAssemblyCleanupSignature());
    }

    public void HasCorrectClassOrAssemblyCleanupSignatureShouldReturnTrueForAsyncTestMethods()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticAsyncTaskMethod");
        Verify(methodInfo.HasCorrectClassOrAssemblyCleanupSignature());
    }

    public void HasCorrectClassOrAssemblyCleanupSignatureShouldReturnTrueForTestMethodsWithoutAsync()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticNonAsyncTaskMethod");
        Verify(methodInfo.HasCorrectClassOrAssemblyCleanupSignature());
    }

    public void HasCorrectClassOrAssemblyCleanupSignatureShouldReturnFalseForAsyncTestMethodsWithNonTaskReturnTypes()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticAsyncVoidMethod");
        Verify(!methodInfo.HasCorrectClassOrAssemblyCleanupSignature());
    }

    #endregion

    #region HasCorrectTestInitializeOrCleanupSignature tests

    public void HasCorrectTestInitializeOrCleanupSignatureShouldReturnFalseForStaticMethods()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticMethod");
        Verify(!methodInfo.HasCorrectTestInitializeOrCleanupSignature());
    }

    public void HasCorrectTestInitializeOrCleanupSignatureShouldReturnFalseForNonPublicMethods()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("InternalMethod", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        Verify(!methodInfo.HasCorrectTestInitializeOrCleanupSignature());
    }

    public void HasCorrectTestInitializeOrCleanupSignatureShouldReturnFalseForMethodsHavingParameters()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("PublicMethodWithInt");
        Verify(!methodInfo.HasCorrectTestInitializeOrCleanupSignature());
    }

    public void HasCorrectTestInitializeOrCleanupSignatureShouldReturnFalseForMethodsNotHavingVoidOrAsyncReturnType()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("PublicMethodReturningInt");
        Verify(!methodInfo.HasCorrectTestInitializeOrCleanupSignature());
    }

    public void HasCorrectTestInitializeOrCleanupSignatureShouldReturnTrueForTestMethods()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("PublicMethod");
        Verify(methodInfo.HasCorrectTestInitializeOrCleanupSignature());
    }

    public void HasCorrectTestInitializeOrCleanupSignatureShouldReturnTrueForAsyncTestMethods()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("PublicAsyncTaskMethod");
        Verify(methodInfo.HasCorrectTestInitializeOrCleanupSignature());
    }

    public void HasCorrectTestInitializeOrCleanupSignatureShouldReturnTrueForTestMethodsWithoutAsync()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("PublicNonAsyncTaskMethod");
        Verify(methodInfo.HasCorrectTestInitializeOrCleanupSignature());
    }

    public void HasCorrectTestInitializeOrCleanupSignatureShouldReturnFalseForAsyncTestMethodsWithNonTaskReturnTypes()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("PublicAsyncVoidMethod");
        Verify(!methodInfo.HasCorrectTestInitializeOrCleanupSignature());
    }

    #endregion

    #region HasCorrectTestMethodSignature tests

    public void HasCorrectTestMethodSignatureShouldReturnFalseForAbstractMethods()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("PublicAbstractMethod");
        Verify(!methodInfo.HasCorrectTestMethodSignature(false));
    }

    public void HasCorrectTestMethodSignatureShouldReturnFalseForStaticMethods()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticMethod");
        Verify(!methodInfo.HasCorrectTestMethodSignature(false));
    }

    public void HasCorrectTestMethodSignatureShouldReturnFalseForGenericMethods()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("PublicGenericMethod");
        Verify(!methodInfo.HasCorrectTestMethodSignature(false));
    }

    public void HasCorrectTestMethodSignatureShouldReturnFalseForNonPublicMethods()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("InternalMethod", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        Verify(!methodInfo.HasCorrectTestMethodSignature(false));
    }

    public void HasCorrectTestMethodSignatureShouldReturnFalseForMethodsHavingParamters()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("PublicMethodWithInt");
        Verify(!methodInfo.HasCorrectTestMethodSignature(false));
    }

    public void HasCorrectTestMethodSignatureShouldReturnTrueForMethodsWithParametersWhenParamterCountIsIgnored()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("PublicMethodWithInt");
        Verify(methodInfo.HasCorrectTestMethodSignature(true));
    }

    public void HasCorrectTestMethodSignatureShouldReturnTrueForTestMethods()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("PublicMethod");
        Verify(methodInfo.HasCorrectTestMethodSignature(false));
    }

    public void HasCorrectTestMethodSignatureShouldReturnTrueForAsyncTestMethods()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("PublicAsyncTaskMethod");
        Verify(methodInfo.HasCorrectTestMethodSignature(false));
    }

    public void HasCorrectTestMethodSignatureShouldReturnTrueForTaskTestMethodsWithoutAsync()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("PublicNonAsyncTaskMethod");
        Verify(methodInfo.HasCorrectTestMethodSignature(false));
    }

    public void HasCorrectTestMethodSignatureShouldReturnFalseForAsyncTestMethodsWithNonTaskReturnTypes()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("PublicAsyncVoidMethod");
        Verify(!methodInfo.HasCorrectTestMethodSignature(false));
    }

    #endregion

    #region HasCorrectTimeout tests

    public void HasCorrectTimeoutShouldReturnFalseForMethodsWithoutTimeoutAttribute()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("PublicMethod");
        Verify(!methodInfo.HasCorrectTimeout());
    }

    public void HasCorrectTimeoutShouldReturnFalseForMethodsWithInvalidTimeoutAttribute()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("PublicMethodWithInvalidTimeout");
        Verify(!methodInfo.HasCorrectTimeout());
    }

    public void HasCorrectTimeoutShouldReturnTrueForMethodsWithTimeoutAttribute()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("PublicMethodWithTimeout");
        Verify(methodInfo.HasCorrectTimeout());
    }

    #endregion

    #region IsVoidOrTaskReturnType tests

    public void IsVoidOrTaskReturnTypeShouldReturnTrueForVoidMethods()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("PublicMethod");
        Verify(methodInfo.IsVoidOrTaskReturnType());
    }

    public void IsVoidOrTaskReturnTypeShouldReturnTrueForAsyncTaskMethods()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("PublicAsyncTaskMethod");
        Verify(methodInfo.IsVoidOrTaskReturnType());
    }

    public void IsVoidOrTaskReturnTypeShouldReturnTrueForTaskMethodsWithoutAsync()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("PublicNonAsyncTaskMethod");
        Verify(methodInfo.IsVoidOrTaskReturnType());
    }

    public void IsVoidOrTaskReturnTypeShouldReturnFalseForNonVoidMethods()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("PublicMethodReturningInt");
        Verify(!methodInfo.IsVoidOrTaskReturnType());
    }

    public void IsVoidOrTaskReturnTypeShouldReturnTrueForAsyncNonTaskMethods()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("PublicAsyncVoidMethod");
        Verify(!methodInfo.IsVoidOrTaskReturnType());
    }

    #endregion

    #region GetAsyncTypeName tests

    public void GetAsyncTypeNameShouldReturnNullForVoidMethods()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("PublicMethod");
        Verify(methodInfo.GetAsyncTypeName() is null);
    }

    public void GetAsyncTypeNameShouldReturnStateMachineTypeNameForAsyncMethods()
    {
        var methodInfo = typeof(DummyTestClass).GetMethod("PublicAsyncVoidMethod");
        Verify(methodInfo.GetAsyncTypeName().StartsWith("Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Extensions.MethodInfoExtensionsTests+DummyTestClass+<PublicAsyncVoidMethod>", StringComparison.Ordinal));
    }

    #endregion

    #region InvokeAsSynchronousTask tests

    public void MethodInfoInvokeAsSynchronousTaskWaitsForCompletionOfAMethodWhichReturnsTask()
    {
        var testMethodCalled = false;
        DummyTestClass2.DummyAsyncMethodBody = (x, y) => Task.Run(
            () =>
            {
                Verify(x == 10);
                Verify(y == 20);
                testMethodCalled = true;
            });

        var dummyTestClass = new DummyTestClass2();
        var dummyAsyncMethod = typeof(DummyTestClass2).GetMethod("DummyAsyncMethod");

        dummyAsyncMethod.InvokeAsSynchronousTask(dummyTestClass, 10, 20);

        Verify(testMethodCalled);
    }

    public void MethodInfoInvokeAsSynchronousTaskExecutesAMethodWhichDoesNotReturnATask()
    {
        var testMethodCalled = false;
        DummyTestClass2.DummyMethodBody = (x, y) =>
        {
            Verify(x == 10);
            Verify(y == 20);
            testMethodCalled = true;
            return true;
        };

        var methodInfo = typeof(DummyTestClass).GetMethod("PublicMethod");

        var dummyTestClass = new DummyTestClass2();
        var dummyMethod = typeof(DummyTestClass2).GetMethod("DummyMethod");

        dummyMethod.InvokeAsSynchronousTask(dummyTestClass, 10, 20);

        Verify(testMethodCalled);
    }

    public void InvokeAsSynchronousShouldThrowIfParametersWereExpectedButWereNotProvided()
    {
        var dummyTestClass = new DummyTestClass2();
        var dummyMethod = typeof(DummyTestClass2).GetMethod("PublicMethodWithParameters");
        try
        {
            // Should throw exception of type TestFailedException
            dummyMethod.InvokeAsSynchronousTask(dummyTestClass, null);
        }
        catch (TestFailedException ex)
        {
            Verify(ex.Outcome == UnitTestOutcome.Error);
            Verify(ex.TryGetMessage() == Resource.UTA_TestMethodExpectedParameters);
        }
    }

    public void InvokeAsSynchronousShouldNotThrowIfParametersWereExpectedAndWereProvided()
    {
        var dummyTestClass = new DummyTestClass2();
        var dummyMethod = typeof(DummyTestClass2).GetMethod("PublicMethodWithParameters");

        void Action() => dummyMethod.InvokeAsSynchronousTask(dummyTestClass, 10, 20);
        Action();
    }

    #endregion

    #region Dummy Class

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
            await DummyAsyncMethodBody(x, y).ConfigureAwait(false);
        }

        public void PublicMethodWithParameters(int x, int y)
        {
            Verify((object)x is not null);
            Verify((object)y is not null);
        }
    }

    private abstract class DummyTestClass
    {
        public static void PublicStaticMethod()
        {
        }

        public static void PublicStaticMethodWithInt(int a)
        {
        }

        public static int PublicStaticMethodWithTCReturningInt(UTFExtension.TestContext tc)
        {
            return 0;
        }

        public static void PublicStaticMethodWithTC(UTFExtension.TestContext tc)
        {
        }

        public static async Task PublicStaticAsyncTaskMethodWithTC(UTFExtension.TestContext tc)
        {
            await Task.FromResult(true).ConfigureAwait(false);
        }

        public static Task PublicStaticNonAsyncTaskMethodWithTC(UTFExtension.TestContext tc)
        {
            return Task.FromResult(true);
        }

        [SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "Done on purpose")]
        public static async void PublicStaticAsyncVoidMethodWithTC(UTFExtension.TestContext tc)
        {
            await Task.FromResult(true).ConfigureAwait(false);
        }

        public static int PublicStaticMethodReturningInt()
        {
            return 0;
        }

        public static async Task PublicStaticAsyncTaskMethod()
        {
            await Task.FromResult(true).ConfigureAwait(false);
        }

        [SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "Done on purpose")]
        public static async void PublicStaticAsyncVoidMethod()
        {
            await Task.FromResult(true).ConfigureAwait(false);
        }

        public static Task PublicStaticNonAsyncTaskMethod()
        {
            return Task.FromResult(true);
        }

        public void PublicMethod()
        {
        }

        public abstract void PublicAbstractMethod();

        public void PublicGenericMethod<T>(T i)
        {
        }

        public void PublicMethodWithInt(int a)
        {
        }

        public int PublicMethodReturningInt()
        {
            return 0;
        }

        public async Task PublicAsyncTaskMethod()
        {
            await Task.FromResult(true).ConfigureAwait(false);
        }

        [SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "Done on purpose")]
        public async void PublicAsyncVoidMethod()
        {
            await Task.FromResult(true).ConfigureAwait(false);
        }

        public Task PublicNonAsyncTaskMethod()
        {
            return Task.FromResult(true);
        }

        [UTF.Timeout(-11)]
        public void PublicMethodWithInvalidTimeout()
        {
        }

        [UTF.Timeout(11)]
        public void PublicMethodWithTimeout()
        {
        }

        internal static void InternalStaticMethod()
        {
        }

        internal void InternalMethod()
        {
        }
    }

    #endregion
}
