// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
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
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicMethod");
        Verify(!methodInfo.HasCorrectClassOrAssemblyInitializeSignature());
    }

    public void HasCorrectClassOrAssemblyInitializeSignatureShouldReturnFalseForNonPublicMethods()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("InternalStaticMethod", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
        Verify(!methodInfo.HasCorrectClassOrAssemblyInitializeSignature());
    }

    public void HasCorrectClassOrAssemblyInitializeSignatureShouldReturnFalseForMethodsNotHavingOneParameter()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticMethod");
        Verify(!methodInfo.HasCorrectClassOrAssemblyInitializeSignature());
    }

    public void HasCorrectClassOrAssemblyInitializeSignatureShouldReturnFalseForMethodsNotTestContextParameter()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticMethodWithInt");
        Verify(!methodInfo.HasCorrectClassOrAssemblyInitializeSignature());
    }

    public void HasCorrectClassOrAssemblyInitializeSignatureShouldReturnFalseForMethodsNotHavingVoidOrAsyncReturnType()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticMethodWithTCReturningInt");
        Verify(!methodInfo.HasCorrectClassOrAssemblyInitializeSignature());
    }

    public void HasCorrectClassOrAssemblyInitializeSignatureShouldReturnTrueForTestMethods()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticMethodWithTC");
        Verify(methodInfo.HasCorrectClassOrAssemblyInitializeSignature());
    }

    public void HasCorrectClassOrAssemblyInitializeSignatureShouldReturnTrueForAsyncTestMethods()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticAsyncTaskMethodWithTC");
        Verify(methodInfo.HasCorrectClassOrAssemblyInitializeSignature());
    }

    public void HasCorrectClassOrAssemblyInitializeSignatureShouldReturnTrueForTestMethodsWithoutAsync()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticNonAsyncTaskMethodWithTC");
        Verify(methodInfo.HasCorrectClassOrAssemblyInitializeSignature());
    }

    public void HasCorrectClassOrAssemblyInitializeSignatureShouldReturnFalseForAsyncTestMethodsWithNonTaskReturnTypes()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticAsyncVoidMethodWithTC");
        Verify(!methodInfo.HasCorrectClassOrAssemblyInitializeSignature());
    }

    #endregion

    #region HasCorrectClassOrAssemblyCleanupSignature tests

    public void HasCorrectClassOrAssemblyCleanupSignatureShouldReturnFalseForNonStaticMethods()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicMethod");
        Verify(!methodInfo.HasCorrectClassOrAssemblyCleanupSignature());
    }

    public void HasCorrectClassOrAssemblyCleanupSignatureShouldReturnFalseForNonPublicMethods()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("InternalStaticMethod", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
        Verify(!methodInfo.HasCorrectClassOrAssemblyCleanupSignature());
    }

    public void HasCorrectClassOrAssemblyCleanupSignatureShouldReturnFalseForMethodsHavingParameters()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticMethodWithInt");
        Verify(!methodInfo.HasCorrectClassOrAssemblyCleanupSignature());
    }

    public void HasCorrectClassOrAssemblyCleanupSignatureShouldReturnFalseForMethodsNotHavingVoidOrAsyncReturnType()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticMethodReturningInt");
        Verify(!methodInfo.HasCorrectClassOrAssemblyCleanupSignature());
    }

    public void HasCorrectClassOrAssemblyCleanupSignatureShouldReturnTrueForTestMethods()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticMethod");
        Verify(methodInfo.HasCorrectClassOrAssemblyCleanupSignature());
    }

    public void HasCorrectClassOrAssemblyCleanupSignatureShouldReturnTrueForAsyncTestMethods()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticAsyncTaskMethod");
        Verify(methodInfo.HasCorrectClassOrAssemblyCleanupSignature());
    }

    public void HasCorrectClassOrAssemblyCleanupSignatureShouldReturnTrueForTestMethodsWithoutAsync()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticNonAsyncTaskMethod");
        Verify(methodInfo.HasCorrectClassOrAssemblyCleanupSignature());
    }

    public void HasCorrectClassOrAssemblyCleanupSignatureShouldReturnFalseForAsyncTestMethodsWithNonTaskReturnTypes()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticAsyncVoidMethod");
        Verify(!methodInfo.HasCorrectClassOrAssemblyCleanupSignature());
    }

    #endregion

    #region HasCorrectTestInitializeOrCleanupSignature tests

    public void HasCorrectTestInitializeOrCleanupSignatureShouldReturnFalseForStaticMethods()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticMethod");
        Verify(!methodInfo.HasCorrectTestInitializeOrCleanupSignature());
    }

    public void HasCorrectTestInitializeOrCleanupSignatureShouldReturnFalseForNonPublicMethods()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("InternalMethod", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        Verify(!methodInfo.HasCorrectTestInitializeOrCleanupSignature());
    }

    public void HasCorrectTestInitializeOrCleanupSignatureShouldReturnFalseForMethodsHavingParameters()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicMethodWithInt");
        Verify(!methodInfo.HasCorrectTestInitializeOrCleanupSignature());
    }

    public void HasCorrectTestInitializeOrCleanupSignatureShouldReturnFalseForMethodsNotHavingVoidOrAsyncReturnType()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicMethodReturningInt");
        Verify(!methodInfo.HasCorrectTestInitializeOrCleanupSignature());
    }

    public void HasCorrectTestInitializeOrCleanupSignatureShouldReturnTrueForTestMethods()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicMethod");
        Verify(methodInfo.HasCorrectTestInitializeOrCleanupSignature());
    }

    public void HasCorrectTestInitializeOrCleanupSignatureShouldReturnTrueForAsyncTestMethods()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicAsyncTaskMethod");
        Verify(methodInfo.HasCorrectTestInitializeOrCleanupSignature());
    }

    public void HasCorrectTestInitializeOrCleanupSignatureShouldReturnTrueForTestMethodsWithoutAsync()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicNonAsyncTaskMethod");
        Verify(methodInfo.HasCorrectTestInitializeOrCleanupSignature());
    }

    public void HasCorrectTestInitializeOrCleanupSignatureShouldReturnFalseForAsyncTestMethodsWithNonTaskReturnTypes()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicAsyncVoidMethod");
        Verify(!methodInfo.HasCorrectTestInitializeOrCleanupSignature());
    }

    #endregion

    #region HasCorrectTestMethodSignature tests

    public void HasCorrectTestMethodSignatureShouldReturnFalseForAbstractMethods()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicAbstractMethod");
        Verify(!methodInfo.HasCorrectTestMethodSignature(false));
    }

    public void HasCorrectTestMethodSignatureShouldReturnFalseForStaticMethods()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticMethod");
        Verify(!methodInfo.HasCorrectTestMethodSignature(false));
    }

    public void HasCorrectTestMethodSignatureShouldReturnFalseForGenericMethods()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicGenericMethod");
        Verify(!methodInfo.HasCorrectTestMethodSignature(false));
    }

    public void HasCorrectTestMethodSignatureShouldReturnFalseForNonPublicMethods()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("InternalMethod", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        Verify(!methodInfo.HasCorrectTestMethodSignature(false));
    }

    public void HasCorrectTestMethodSignatureShouldReturnFalseForMethodsHavingParamters()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicMethodWithInt");
        Verify(!methodInfo.HasCorrectTestMethodSignature(false));
    }

    public void HasCorrectTestMethodSignatureShouldReturnTrueForMethodsWithParametersWhenParamterCountIsIgnored()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicMethodWithInt");
        Verify(methodInfo.HasCorrectTestMethodSignature(true));
    }

    public void HasCorrectTestMethodSignatureShouldReturnTrueForTestMethods()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicMethod");
        Verify(methodInfo.HasCorrectTestMethodSignature(false));
    }

    public void HasCorrectTestMethodSignatureShouldReturnTrueForAsyncTestMethods()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicAsyncTaskMethod");
        Verify(methodInfo.HasCorrectTestMethodSignature(false));
    }

    public void HasCorrectTestMethodSignatureShouldReturnTrueForTaskTestMethodsWithoutAsync()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicNonAsyncTaskMethod");
        Verify(methodInfo.HasCorrectTestMethodSignature(false));
    }

    public void HasCorrectTestMethodSignatureShouldReturnFalseForAsyncTestMethodsWithNonTaskReturnTypes()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicAsyncVoidMethod");
        Verify(!methodInfo.HasCorrectTestMethodSignature(false));
    }

    #endregion

    #region HasCorrectTimeout tests

    public void HasCorrectTimeoutShouldReturnFalseForMethodsWithoutTimeoutAttribute()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicMethod");
        Verify(!methodInfo.HasCorrectTimeout());
    }

    public void HasCorrectTimeoutShouldReturnFalseForMethodsWithInvalidTimeoutAttribute()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicMethodWithInvalidTimeout");
        Verify(!methodInfo.HasCorrectTimeout());
    }

    public void HasCorrectTimeoutShouldReturnTrueForMethodsWithTimeoutAttribute()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicMethodWithTimeout");
        Verify(methodInfo.HasCorrectTimeout());
    }

    #endregion

    #region IsVoidOrTaskReturnType tests

    public void IsVoidOrTaskReturnTypeShouldReturnTrueForVoidMethods()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicMethod");
        Verify(methodInfo.IsValidReturnType());
    }

    public void IsVoidOrTaskReturnTypeShouldReturnTrueForAsyncTaskMethods()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicAsyncTaskMethod");
        Verify(methodInfo.IsValidReturnType());
    }

    public void IsVoidOrTaskReturnTypeShouldReturnTrueForTaskMethodsWithoutAsync()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicNonAsyncTaskMethod");
        Verify(methodInfo.IsValidReturnType());
    }

    public void IsVoidOrTaskReturnTypeShouldReturnFalseForNonVoidMethods()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicMethodReturningInt");
        Verify(!methodInfo.IsValidReturnType());
    }

    public void IsVoidOrTaskReturnTypeShouldReturnTrueForAsyncNonTaskMethods()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicAsyncVoidMethod");
        Verify(!methodInfo.IsValidReturnType());
    }

    #endregion

    #region GetAsyncTypeName tests

    public void GetAsyncTypeNameShouldReturnNullForVoidMethods()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicMethod");
        Verify(methodInfo.GetAsyncTypeName() is null);
    }

    public void GetAsyncTypeNameShouldReturnStateMachineTypeNameForAsyncMethods()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicAsyncVoidMethod");
        Verify(methodInfo.GetAsyncTypeName().StartsWith("Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Extensions.MethodInfoExtensionsTests+DummyTestClass+<PublicAsyncVoidMethod>", StringComparison.Ordinal));
    }

    #endregion

    #region InvokeAsSynchronousTask tests

    public void MethodInfoInvokeAsSynchronousTaskWaitsForCompletionOfAMethodWhichReturnsTask()
    {
        bool testMethodCalled = false;
        DummyTestClass2.DummyAsyncMethodBody = (x, y) => Task.Run(
            () =>
            {
                Verify(x == 10);
                Verify(y == 20);
                testMethodCalled = true;
            });

        var dummyTestClass = new DummyTestClass2();
        MethodInfo dummyAsyncMethod = typeof(DummyTestClass2).GetMethod("DummyAsyncMethod");

        dummyAsyncMethod.InvokeAsSynchronousTask(dummyTestClass, 10, 20);

        Verify(testMethodCalled);
    }

    public void MethodInfoInvokeAsSynchronousTaskExecutesAMethodWhichDoesNotReturnATask()
    {
        bool testMethodCalled = false;
        DummyTestClass2.DummyMethodBody = (x, y) =>
        {
            Verify(x == 10);
            Verify(y == 20);
            testMethodCalled = true;
            return true;
        };

        var dummyTestClass = new DummyTestClass2();
        MethodInfo dummyMethod = typeof(DummyTestClass2).GetMethod("DummyMethod");

        dummyMethod.InvokeAsSynchronousTask(dummyTestClass, 10, 20);

        Verify(testMethodCalled);
    }

    public void InvokeAsSynchronousShouldThrowIfParametersWereExpectedButWereNotProvided()
    {
        var dummyTestClass = new DummyTestClass2();
        MethodInfo dummyMethod = typeof(DummyTestClass2).GetMethod("PublicMethodWithParameters");
        try
        {
            // Should throw exception of type TestFailedException
            dummyMethod.InvokeAsSynchronousTask(dummyTestClass, null);
        }
        catch (TestFailedException ex)
        {
            Verify(ex.Outcome == UnitTestOutcome.Error);
            Verify(ex.TryGetMessage() == string.Format(CultureInfo.InvariantCulture, Resource.CannotRunTestMethodNoDataError, "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Extensions.MethodInfoExtensionsTests+DummyTestClass2", "PublicMethodWithParameters"));
        }
    }

    public void InvokeAsSynchronousShouldNotThrowIfParametersWereExpectedAndWereProvided()
    {
        var dummyTestClass = new DummyTestClass2();
        MethodInfo dummyMethod = typeof(DummyTestClass2).GetMethod("PublicMethodWithParameters");

        void Action() => dummyMethod.InvokeAsSynchronousTask(dummyTestClass, 10, 20);
        Action();
    }

    public void InvokeAsSynchronousShouldThrowIfParametersWereExpectedButIncorrectCountOfParametersWasProvided()
    {
        var dummyTestClass = new DummyTestClass2();
        MethodInfo dummyMethod = typeof(DummyTestClass2).GetMethod("PublicMethodWithParameters");
        try
        {
            // Should throw exception of type TestFailedException
            dummyMethod.InvokeAsSynchronousTask(dummyTestClass, 1);
        }
        catch (TestFailedException ex)
        {
            Verify(ex.Outcome == UnitTestOutcome.Error);

            // Error in English is:
            //    Cannot run test method 'Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Extensions.MethodInfoExtensionsTests+DummyTestClass2.PublicMethodWithParameters': Test data doesn't match method parameters. Either the count or types are different.
            //    Test expected 2 parameter(s), with types 'Int32, Int32',
            //    but received 1 argument(s), with types 'Int32'.
            Verify(ex.TryGetMessage() == string.Format(CultureInfo.InvariantCulture, Resource.CannotRunTestArgumentsMismatchError, "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Extensions.MethodInfoExtensionsTests+DummyTestClass2", "PublicMethodWithParameters",
                2, "Int32, Int32",
                1, "Int32"));
        }
    }

    public void InvokeAsSynchronousShouldThrowIfParametersWereExpectedButIncorrectTypesOfParametersWereProvided()
    {
        var dummyTestClass = new DummyTestClass2();
        MethodInfo dummyMethod = typeof(DummyTestClass2).GetMethod("PublicMethodWithParameters");
        try
        {
            // Should throw exception of type TestFailedException
            dummyMethod.InvokeAsSynchronousTask(dummyTestClass, "10", "20");
        }
        catch (TestFailedException ex)
        {
            Verify(ex.Outcome == UnitTestOutcome.Error);

            // Error in English is:
            //    Cannot run test method 'Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Extensions.MethodInfoExtensionsTests+DummyTestClass2.PublicMethodWithParameters': Test data doesn't match method parameters. Either the count or types are different.
            //    Test expected 2 parameter(s), with types 'Int32, Int32',
            //    but received 2 argument(s), with types 'String, String'.
            Verify(ex.TryGetMessage() == string.Format(CultureInfo.InvariantCulture, Resource.CannotRunTestArgumentsMismatchError, "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Extensions.MethodInfoExtensionsTests+DummyTestClass2", "PublicMethodWithParameters",
                2, "Int32, Int32",
                2, "String, String"));
        }
    }

    public void InvokeAsSynchronousShouldNotThrowIfParametersWereExpectedAndTheProvidedParametersCanImplicitlyConvertToTheExpectedParameters()
    {
        var dummyTestClass = new DummyTestClass2();
        MethodInfo dummyMethod = typeof(DummyTestClass2).GetMethod("PublicMethodWithParameters");

        // The receiving method expects int, but we provide byte, which converts to int implicitly.
        // If this test fails we are checking the parameters too much, and should rather let the runtime
        // do its work.
        byte ten = 10;
        byte twenty = 20;
        void Action() => dummyMethod.InvokeAsSynchronousTask(dummyTestClass, ten, twenty);
        Action();
    }

    #endregion

    #region Dummy Class

    public class DummyTestClass2
    {
        public static Func<int, int, Task> DummyAsyncMethodBody { get; set; }

        public static Func<int, int, bool> DummyMethodBody { get; set; }

        public bool DummyMethod(int x, int y) => DummyMethodBody(x, y);

        public async Task DummyAsyncMethod(int x, int y) => await DummyAsyncMethodBody(x, y).ConfigureAwait(false);

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

        public static int PublicStaticMethodWithTCReturningInt(UTFExtension.TestContext tc) => 0;

        public static void PublicStaticMethodWithTC(UTFExtension.TestContext tc)
        {
        }

        public static async Task PublicStaticAsyncTaskMethodWithTC(UTFExtension.TestContext tc) => await Task.FromResult(true).ConfigureAwait(false);

        public static Task PublicStaticNonAsyncTaskMethodWithTC(UTFExtension.TestContext tc) => Task.FromResult(true);

        [SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "Done on purpose")]
        public static async void PublicStaticAsyncVoidMethodWithTC(UTFExtension.TestContext tc) => await Task.FromResult(true).ConfigureAwait(false);

        public static int PublicStaticMethodReturningInt() => 0;

        public static async Task PublicStaticAsyncTaskMethod() => await Task.FromResult(true).ConfigureAwait(false);

        [SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "Done on purpose")]
        public static async void PublicStaticAsyncVoidMethod() => await Task.FromResult(true).ConfigureAwait(false);

        public static Task PublicStaticNonAsyncTaskMethod() => Task.FromResult(true);

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

        public int PublicMethodReturningInt() => 0;

        public async Task PublicAsyncTaskMethod() => await Task.FromResult(true).ConfigureAwait(false);

        [SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "Done on purpose")]
        public async void PublicAsyncVoidMethod() => await Task.FromResult(true).ConfigureAwait(false);

        public Task PublicNonAsyncTaskMethod() => Task.FromResult(true);

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
