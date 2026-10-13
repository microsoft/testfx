// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Extensions;

public class MethodInfoExtensionsTests : TestContainer
{
    public async Task GenericInvocationInfersReorderedParametersAndSkipsNullArguments()
    {
        GenericInvocationFixture fixture = new();
        MethodInfo reordered = typeof(GenericInvocationFixture).GetMethod(nameof(GenericInvocationFixture.Reordered))!;
        (await (Task<string>)reordered.GetInvokeResultAsync(fixture, 7, "text")!).Should().Be("String:Int32:text:7");
        MethodInfo same = typeof(GenericInvocationFixture).GetMethod(nameof(GenericInvocationFixture.Same))!;
        (await (Task<string>)same.GetInvokeResultAsync(fixture, null, "text")!).Should().Be("String::text");
        (await (Task<string>)same.GetInvokeResultAsync(fixture, "text", null)!).Should().Be("String:text:");
    }

    public async Task GenericInvocationChoosesAssignableBaseRegardlessOfArgumentOrder()
    {
        GenericInvocationFixture fixture = new();
        MethodInfo method = typeof(GenericInvocationFixture).GetMethod(nameof(GenericInvocationFixture.Same))!;
        (await (Task<string>)method.GetInvokeResultAsync(fixture, new ChildValue(), new ParentValue())!).Should().Be("ParentValue:child:parent");
        (await (Task<string>)method.GetInvokeResultAsync(fixture, new ParentValue(), new ChildValue())!).Should().Be("ParentValue:parent:child");
    }

    public async Task GenericInvocationInfersArrayElementsAndNestedGenericArguments()
    {
        GenericInvocationFixture fixture = new();
        MethodInfo array = typeof(GenericInvocationFixture).GetMethod(nameof(GenericInvocationFixture.Array))!;
        (await (Task<string>)array.GetInvokeResultAsync(fixture, new int[] { 2, 3 })!).Should().Be("Int32:2");
        MethodInfo nested = typeof(GenericInvocationFixture).GetMethod(nameof(GenericInvocationFixture.Nested))!;
        Dictionary<string, List<int>> values = new() { ["key"] = [2, 3] };
        (await (Task<string>)nested.GetInvokeResultAsync(fixture, values)!).Should().Be("String:Int32:5");
        MethodInfo enumerable = typeof(GenericInvocationFixture).GetMethod(nameof(GenericInvocationFixture.Enumerable))!;
        (await (Task<string>)enumerable.GetInvokeResultAsync(fixture, new List<int> { 2, 3 })!).Should().Be("Int32:5");
    }

    public void GenericInvocationRejectsConflictingOrUninferableTypePartitions()
    {
        GenericInvocationFixture fixture = new();
        MethodInfo same = typeof(GenericInvocationFixture).GetMethod(nameof(GenericInvocationFixture.Same))!;
        Action conflict = () => _ = same.GetInvokeResultAsync(fixture, 7, "text");
        conflict.Should().Throw<InvalidOperationException>().WithMessage("*System.Int32*System.String*");
        Action nullOnly = () => _ = same.GetInvokeResultAsync(fixture, new object?[] { null, null });
        nullOnly.Should().Throw<InvalidOperationException>().WithMessage("*T*");
        MethodInfo enumerable = typeof(GenericInvocationFixture).GetMethod(nameof(GenericInvocationFixture.Enumerable))!;
        Action differentShape = () => _ = enumerable.GetInvokeResultAsync(fixture, new int[] { 2, 3 });
        differentShape.Should().Throw<InvalidOperationException>().WithMessage("*T*");
        MethodInfo noParameters = typeof(GenericInvocationFixture).GetMethod(nameof(GenericInvocationFixture.NoParameters))!;
        Action noArguments = () => _ = noParameters.GetInvokeResultAsync(fixture, arguments: null);
        noArguments.Should().Throw<TestFailedException>().Which.Outcome.Should().Be(UnitTestOutcome.Error);
    }

    public void GenericInvocationPreservesConstraintFailureAsTestErrorWithOriginalCause()
    {
        GenericInvocationFixture fixture = new();
        MethodInfo constrained = typeof(GenericInvocationFixture).GetMethod(nameof(GenericInvocationFixture.ClassOnly))!;
        Action invoke = () => _ = constrained.GetInvokeResultAsync(fixture, 7);

        TestFailedException failure = invoke.Should().Throw<TestFailedException>().Which;
        failure.Outcome.Should().Be(UnitTestOutcome.Error);
        failure.InnerException.Should().BeOfType<ArgumentException>();
        failure.Message.Should().Contain(nameof(GenericInvocationFixture.ClassOnly)).And.Contain("System.Int32");
    }

    private class ParentValue
    {
        public override string ToString() => "parent";
    }

    private sealed class ChildValue : ParentValue
    {
        public override string ToString() => "child";
    }

    private sealed class GenericInvocationFixture
    {
        public Task<string> Same<T>(T first, T second) => Task.FromResult($"{typeof(T).Name}:{first}:{second}");

        public Task<string> Reordered<TFirst, TSecond>(TSecond first, TFirst second)
            => Task.FromResult($"{typeof(TFirst).Name}:{typeof(TSecond).Name}:{second}:{first}");

        public Task<string> Array<T>(T[] values) => Task.FromResult($"{typeof(T).Name}:{values.Length}");

        public Task<string> Nested<TKey, TValue>(Dictionary<TKey, List<TValue>> values)
            where TKey : notnull
            => Task.FromResult($"{typeof(TKey).Name}:{typeof(TValue).Name}:{values.Values.SelectMany(value => value).Sum(value => Convert.ToInt32(value, CultureInfo.InvariantCulture))}");

        public Task<string> Enumerable<T>(IEnumerable<T> values)
            => Task.FromResult($"{typeof(T).Name}:{values.Sum(value => Convert.ToInt32(value, CultureInfo.InvariantCulture))}");

        public void NoParameters<T>()
        {
        }

        public void ClassOnly<T>(T value)
            where T : class
        {
        }
    }

    #region HasCorrectClassOrAssemblyInitializeSignature tests

    public void HasCorrectClassOrAssemblyInitializeSignatureShouldReturnFalseForNonStaticMethods()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicMethod")!;
        methodInfo.HasCorrectClassOrAssemblyInitializeSignature().Should().BeFalse();
    }

    public void HasCorrectClassOrAssemblyInitializeSignatureShouldReturnFalseForNonPublicMethods()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("InternalStaticMethod", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)!;
        methodInfo.HasCorrectClassOrAssemblyInitializeSignature().Should().BeFalse();
    }

    public void HasCorrectClassOrAssemblyInitializeSignatureShouldReturnFalseForMethodsNotHavingOneParameter()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticMethod")!;
        methodInfo.HasCorrectClassOrAssemblyInitializeSignature().Should().BeFalse();
    }

    public void HasCorrectClassOrAssemblyInitializeSignatureShouldReturnFalseForMethodsNotTestContextParameter()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticMethodWithInt")!;
        methodInfo.HasCorrectClassOrAssemblyInitializeSignature().Should().BeFalse();
    }

    public void HasCorrectClassOrAssemblyInitializeSignatureShouldReturnFalseForMethodsNotHavingVoidOrAsyncReturnType()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticMethodWithTCReturningInt")!;
        methodInfo.HasCorrectClassOrAssemblyInitializeSignature().Should().BeFalse();
    }

    public void HasCorrectClassOrAssemblyInitializeSignatureShouldReturnTrueForTestMethods()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticMethodWithTC")!;
        methodInfo.HasCorrectClassOrAssemblyInitializeSignature().Should().BeTrue();
    }

    public void HasCorrectClassOrAssemblyInitializeSignatureShouldReturnTrueForAsyncTestMethods()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticAsyncTaskMethodWithTC")!;
        methodInfo.HasCorrectClassOrAssemblyInitializeSignature().Should().BeTrue();
    }

    public void HasCorrectClassOrAssemblyInitializeSignatureShouldReturnTrueForTestMethodsWithoutAsync()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticNonAsyncTaskMethodWithTC")!;
        methodInfo.HasCorrectClassOrAssemblyInitializeSignature().Should().BeTrue();
    }

    public void HasCorrectClassOrAssemblyInitializeSignatureShouldReturnFalseForAsyncTestMethodsWithNonTaskReturnTypes()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticAsyncVoidMethodWithTC")!;
        methodInfo.HasCorrectClassOrAssemblyInitializeSignature().Should().BeFalse();
    }

    #endregion

    #region HasCorrectClassOrAssemblyCleanupSignature tests

    public void HasCorrectClassOrAssemblyCleanupSignatureShouldReturnFalseForNonStaticMethods()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicMethod")!;
        methodInfo.HasCorrectClassOrAssemblyCleanupSignature().Should().BeFalse();
    }

    public void HasCorrectClassOrAssemblyCleanupSignatureShouldReturnFalseForNonPublicMethods()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("InternalStaticMethod", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)!;
        methodInfo.HasCorrectClassOrAssemblyCleanupSignature().Should().BeFalse();
    }

    public void HasCorrectClassOrAssemblyCleanupSignatureShouldReturnFalseForMethodsHavingParameters()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticMethodWithInt")!;
        methodInfo.HasCorrectClassOrAssemblyCleanupSignature().Should().BeFalse();
    }

    public void HasCorrectClassOrAssemblyCleanupSignatureShouldReturnFalseForMethodsNotHavingVoidOrAsyncReturnType()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticMethodReturningInt")!;
        methodInfo.HasCorrectClassOrAssemblyCleanupSignature().Should().BeFalse();
    }

    public void HasCorrectClassOrAssemblyCleanupSignatureShouldReturnTrueForTestMethods()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticMethod")!;
        methodInfo.HasCorrectClassOrAssemblyCleanupSignature().Should().BeTrue();
    }

    public void HasCorrectClassOrAssemblyCleanupSignatureShouldReturnTrueForAsyncTestMethods()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticAsyncTaskMethod")!;
        methodInfo.HasCorrectClassOrAssemblyCleanupSignature().Should().BeTrue();
    }

    public void HasCorrectClassOrAssemblyCleanupSignatureShouldReturnTrueForTestMethodsWithoutAsync()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticNonAsyncTaskMethod")!;
        methodInfo.HasCorrectClassOrAssemblyCleanupSignature().Should().BeTrue();
    }

    public void HasCorrectClassOrAssemblyCleanupSignatureShouldReturnFalseForAsyncTestMethodsWithNonTaskReturnTypes()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticAsyncVoidMethod")!;
        methodInfo.HasCorrectClassOrAssemblyCleanupSignature().Should().BeFalse();
    }

    #endregion

    #region HasCorrectTestInitializeOrCleanupSignature tests

    public void HasCorrectTestInitializeOrCleanupSignatureShouldReturnFalseForStaticMethods()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticMethod")!;
        methodInfo.HasCorrectTestInitializeOrCleanupSignature().Should().BeFalse();
    }

    public void HasCorrectTestInitializeOrCleanupSignatureShouldReturnFalseForInstanceMethodsOnFSharpModules()
    {
        Type moduleType = FSharpModuleTestFixture.ModuleType;
        MethodInfo methodInfo = moduleType.GetMethod("InstanceMethod")!;
        moduleType.IsFSharpModule().Should().BeTrue();

        methodInfo.HasCorrectTestInitializeOrCleanupSignature().Should().BeFalse();
    }

    public void HasCorrectTestInitializeOrCleanupSignatureShouldReturnTrueForStaticMethodsOnFSharpModules()
    {
        Type moduleType = FSharpModuleTestFixture.ModuleType;
        MethodInfo methodInfo = moduleType.GetMethod("StaticMethod")!;
        moduleType.IsFSharpModule().Should().BeTrue();

        methodInfo.HasCorrectTestInitializeOrCleanupSignature().Should().BeTrue();
    }

    public void HasCorrectTestInitializeOrCleanupSignatureShouldReturnFalseForNonPublicMethods()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("InternalMethod", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)!;
        methodInfo.HasCorrectTestInitializeOrCleanupSignature().Should().BeFalse();
    }

    public void HasCorrectTestInitializeOrCleanupSignatureShouldReturnFalseForMethodsHavingParameters()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicMethodWithInt")!;
        methodInfo.HasCorrectTestInitializeOrCleanupSignature().Should().BeFalse();
    }

    public void HasCorrectTestInitializeOrCleanupSignatureShouldReturnFalseForMethodsNotHavingVoidOrAsyncReturnType()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicMethodReturningInt")!;
        methodInfo.HasCorrectTestInitializeOrCleanupSignature().Should().BeFalse();
    }

    public void HasCorrectTestInitializeOrCleanupSignatureShouldReturnTrueForTestMethods()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicMethod")!;
        methodInfo.HasCorrectTestInitializeOrCleanupSignature().Should().BeTrue();
    }

    public void HasCorrectTestInitializeOrCleanupSignatureShouldReturnTrueForAsyncTestMethods()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicAsyncTaskMethod")!;
        methodInfo.HasCorrectTestInitializeOrCleanupSignature().Should().BeTrue();
    }

    public void HasCorrectTestInitializeOrCleanupSignatureShouldReturnTrueForTestMethodsWithoutAsync()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicNonAsyncTaskMethod")!;
        methodInfo.HasCorrectTestInitializeOrCleanupSignature().Should().BeTrue();
    }

    public void HasCorrectTestInitializeOrCleanupSignatureShouldReturnFalseForAsyncTestMethodsWithNonTaskReturnTypes()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicAsyncVoidMethod")!;
        methodInfo.HasCorrectTestInitializeOrCleanupSignature().Should().BeFalse();
    }

    #endregion

    #region HasCorrectTestMethodSignature tests

    public void HasCorrectTestMethodSignatureShouldReturnFalseForAbstractMethods()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicAbstractMethod")!;
        methodInfo.HasCorrectTestMethodSignature(false).Should().BeFalse();
    }

    public void HasCorrectTestMethodSignatureShouldReturnFalseForStaticMethods()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicStaticMethod")!;
        methodInfo.HasCorrectTestMethodSignature(false).Should().BeFalse();
    }

    public void HasCorrectTestMethodSignatureShouldReturnFalseForInstanceMethodsOnFSharpModules()
    {
        Type moduleType = FSharpModuleTestFixture.ModuleType;
        MethodInfo methodInfo = moduleType.GetMethod("InstanceMethod")!;
        moduleType.IsFSharpModule().Should().BeTrue();

        methodInfo.HasCorrectTestMethodSignature(false).Should().BeFalse();
    }

    public void HasCorrectTestMethodSignatureShouldReturnTrueForStaticMethodsOnFSharpModules()
    {
        Type moduleType = FSharpModuleTestFixture.ModuleType;
        MethodInfo methodInfo = moduleType.GetMethod("StaticMethod")!;
        moduleType.IsFSharpModule().Should().BeTrue();

        methodInfo.HasCorrectTestMethodSignature(false).Should().BeTrue();
    }

    public void HasCorrectTestMethodSignatureShouldReturnFalseForGenericMethods()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicGenericMethod")!;
        methodInfo.HasCorrectTestMethodSignature(false).Should().BeFalse();
    }

    public void HasCorrectTestMethodSignatureShouldReturnFalseForNonPublicMethods()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("InternalMethod", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)!;
        methodInfo.HasCorrectTestMethodSignature(false).Should().BeFalse();
    }

    public void HasCorrectTestMethodSignatureShouldReturnFalseForMethodsHavingParameters()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicMethodWithInt")!;
        methodInfo.HasCorrectTestMethodSignature(false).Should().BeFalse();
    }

    public void HasCorrectTestMethodSignatureShouldReturnTrueForMethodsWithParametersWhenParameterCountIsIgnored()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicMethodWithInt")!;
        methodInfo.HasCorrectTestMethodSignature(true).Should().BeTrue();
    }

    public void HasCorrectTestMethodSignatureShouldReturnTrueForTestMethods()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicMethod")!;
        methodInfo.HasCorrectTestMethodSignature(false).Should().BeTrue();
    }

    public void HasCorrectTestMethodSignatureShouldReturnTrueForAsyncTestMethods()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicAsyncTaskMethod")!;
        methodInfo.HasCorrectTestMethodSignature(false).Should().BeTrue();
    }

    public void HasCorrectTestMethodSignatureShouldReturnTrueForTaskTestMethodsWithoutAsync()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicNonAsyncTaskMethod")!;
        methodInfo.HasCorrectTestMethodSignature(false).Should().BeTrue();
    }

    public void HasCorrectTestMethodSignatureShouldReturnFalseForAsyncTestMethodsWithNonTaskReturnTypes()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicAsyncVoidMethod")!;
        methodInfo.HasCorrectTestMethodSignature(false).Should().BeFalse();
    }

    #endregion

    #region HasCorrectTimeout tests

    public void HasCorrectTimeoutShouldReturnFalseForMethodsWithInvalidTimeoutAttribute()
    {
        var timeoutAttribute = new TimeoutAttribute(-11);
        timeoutAttribute.HasCorrectTimeout.Should().BeFalse();
    }

    public void HasCorrectTimeoutShouldReturnTrueForMethodsWithTimeoutAttribute()
    {
        var timeoutAttribute = new TimeoutAttribute(11);
        timeoutAttribute.HasCorrectTimeout.Should().BeTrue();
    }

    #endregion

    #region IsVoidOrTaskReturnType tests

    public void IsVoidOrTaskReturnTypeShouldReturnTrueForVoidMethods()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicMethod")!;
        methodInfo.IsValidReturnType().Should().BeTrue();
    }

    public void IsVoidOrTaskReturnTypeShouldReturnTrueForAsyncTaskMethods()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicAsyncTaskMethod")!;
        methodInfo.IsValidReturnType().Should().BeTrue();
    }

    public void IsVoidOrTaskReturnTypeShouldReturnTrueForTaskMethodsWithoutAsync()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicNonAsyncTaskMethod")!;
        methodInfo.IsValidReturnType().Should().BeTrue();
    }

    public void IsVoidOrTaskReturnTypeShouldReturnFalseForNonVoidMethods()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicMethodReturningInt")!;
        methodInfo.IsValidReturnType().Should().BeFalse();
    }

    public void IsVoidOrTaskReturnTypeShouldReturnTrueForAsyncNonTaskMethods()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicAsyncVoidMethod")!;
        methodInfo.IsValidReturnType().Should().BeFalse();
    }

    #endregion

    #region GetAsyncTypeName tests

    public void GetAsyncTypeNameShouldReturnNullForVoidMethods()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicMethod")!;
        methodInfo.GetAsyncTypeName().Should().BeNull();
    }

    public void GetAsyncTypeNameShouldReturnStateMachineTypeNameForAsyncMethods()
    {
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod("PublicAsyncVoidMethod")!;
        methodInfo.GetAsyncTypeName()!.Should().StartWith("Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Extensions.MethodInfoExtensionsTests+DummyTestClass+<PublicAsyncVoidMethod>");
    }

    #endregion

    #region GetInvokeResultAsync tests

    public async Task MethodInfoGetInvokeResultAsyncTaskWaitsForCompletionOfAMethodWhichReturnsTask()
    {
        bool testMethodCalled = false;
        DummyTestClass2.DummyAsyncMethodBody = (x, y) => Task.Run(
            () =>
            {
                x.Should().Be(10);
                y.Should().Be(20);
                testMethodCalled = true;
            });

        var dummyTestClass = new DummyTestClass2();
        MethodInfo dummyAsyncMethod = typeof(DummyTestClass2).GetMethod("DummyAsyncMethod")!;

        Task? task = dummyAsyncMethod.GetInvokeResultAsync(dummyTestClass, 10, 20);
        if (task is not null)
        {
            await task;
        }

        testMethodCalled.Should().BeTrue();
    }

    public async Task MethodInfoGetInvokeResultAsyncTaskExecutesAMethodWhichDoesNotReturnATask()
    {
        bool testMethodCalled = false;
        DummyTestClass2.DummyMethodBody = (x, y) =>
        {
            x.Should().Be(10);
            y.Should().Be(20);
            testMethodCalled = true;
            return true;
        };

        var dummyTestClass = new DummyTestClass2();
        MethodInfo dummyMethod = typeof(DummyTestClass2).GetMethod("DummyMethod")!;

        Task? task = dummyMethod.GetInvokeResultAsync(dummyTestClass, 10, 20);
        if (task is not null)
        {
            await task;
        }

        testMethodCalled.Should().BeTrue();
    }

    public async Task GetInvokeResultAsyncShouldThrowIfParametersWereExpectedButWereNotProvided()
    {
        var dummyTestClass = new DummyTestClass2();
        MethodInfo dummyMethod = typeof(DummyTestClass2).GetMethod("PublicMethodWithParameters")!;
        try
        {
            // Should throw exception of type TestFailedException
            Task? task = dummyMethod.GetInvokeResultAsync(dummyTestClass, null);
            if (task is not null)
            {
                await task;
            }
        }
        catch (TestFailedException ex)
        {
            ex.Outcome.Should().Be(UnitTestOutcome.Error);
            ex.TryGetMessage().Should().Be(string.Format(CultureInfo.InvariantCulture, Resource.CannotRunTestMethodNoDataError, "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Extensions.MethodInfoExtensionsTests+DummyTestClass2", "PublicMethodWithParameters"));
        }
    }

    public async Task GetInvokeResultAsyncShouldNotThrowIfParametersWereExpectedAndWereProvided()
    {
        var dummyTestClass = new DummyTestClass2();
        MethodInfo dummyMethod = typeof(DummyTestClass2).GetMethod("PublicMethodWithParameters")!;

        Task? task = dummyMethod.GetInvokeResultAsync(dummyTestClass, 10, 20);
        if (task is not null)
        {
            await task;
        }
    }

    public async Task GetInvokeResultAsyncShouldThrowIfParametersWereExpectedButIncorrectCountOfParametersWasProvided()
    {
        var dummyTestClass = new DummyTestClass2();
        MethodInfo dummyMethod = typeof(DummyTestClass2).GetMethod("PublicMethodWithParameters")!;
        try
        {
            // Should throw exception of type TestFailedException
            Task? task = dummyMethod.GetInvokeResultAsync(dummyTestClass, 1);
            if (task is not null)
            {
                await task;
            }
        }
        catch (TestFailedException ex)
        {
            ex.Outcome.Should().Be(UnitTestOutcome.Error);

            // Error in English is:
            //    Cannot run test method 'Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Extensions.MethodInfoExtensionsTests+DummyTestClass2.PublicMethodWithParameters': Test data doesn't match method parameters. Either the count or types are different.
            //    Test expected 2 parameter(s), with types 'Int32, Int32',
            //    but received 1 argument(s), with types 'Int32'.
            ex.TryGetMessage().Should().Be(string.Format(CultureInfo.InvariantCulture, Resource.CannotRunTestArgumentsMismatchError, "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Extensions.MethodInfoExtensionsTests+DummyTestClass2", "PublicMethodWithParameters",
                2, "Int32, Int32",
                1, "Int32"));
        }
    }

    public async Task GetInvokeResultAsyncShouldThrowIfParametersWereExpectedButIncorrectTypesOfParametersWereProvided()
    {
        var dummyTestClass = new DummyTestClass2();
        MethodInfo dummyMethod = typeof(DummyTestClass2).GetMethod("PublicMethodWithParameters")!;
        try
        {
            // Should throw exception of type TestFailedException
            Task? task = dummyMethod.GetInvokeResultAsync(dummyTestClass, "10", "20");
            if (task is not null)
            {
                await task;
            }
        }
        catch (TestFailedException ex)
        {
            ex.Outcome.Should().Be(UnitTestOutcome.Error);

            // Error in English is:
            //    Cannot run test method 'Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Extensions.MethodInfoExtensionsTests+DummyTestClass2.PublicMethodWithParameters': Test data doesn't match method parameters. Either the count or types are different.
            //    Test expected 2 parameter(s), with types 'Int32, Int32',
            //    but received 2 argument(s), with types 'String, String'.
            ex.TryGetMessage().Should().Be(string.Format(CultureInfo.InvariantCulture, Resource.CannotRunTestArgumentsMismatchError, "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Extensions.MethodInfoExtensionsTests+DummyTestClass2", "PublicMethodWithParameters",
                2, "Int32, Int32",
                2, "String, String"));
        }
    }

    public async Task GetInvokeResultAsyncShouldNotThrowIfParametersWereExpectedAndTheProvidedParametersCanImplicitlyConvertToTheExpectedParameters()
    {
        var dummyTestClass = new DummyTestClass2();
        MethodInfo dummyMethod = typeof(DummyTestClass2).GetMethod("PublicMethodWithParameters")!;

        // The receiving method expects int, but we provide byte, which converts to int implicitly.
        // If this test fails we are checking the parameters too much, and should rather let the runtime
        // do its work.
        byte ten = 10;
        byte twenty = 20;
        Task? task = dummyMethod.GetInvokeResultAsync(dummyTestClass, ten, twenty);
        if (task is not null)
        {
            await task;
        }
    }

    #endregion

    #region Dummy Class

    public class DummyTestClass2
    {
        public static Func<int, int, Task> DummyAsyncMethodBody { get; set; } = null!;

        public static Func<int, int, bool> DummyMethodBody { get; set; } = null!;

        public bool DummyMethod(int x, int y) => DummyMethodBody(x, y);

        public async Task DummyAsyncMethod(int x, int y) => await DummyAsyncMethodBody(x, y).ConfigureAwait(false);

        public void PublicMethodWithParameters(int x, int y)
        {
            ((object)x).Should().NotBeNull();
            ((object)y).Should().NotBeNull();
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

        public static int PublicStaticMethodWithTCReturningInt(TestContext tc) => 0;

        public static void PublicStaticMethodWithTC(TestContext tc)
        {
        }

        public static async Task PublicStaticAsyncTaskMethodWithTC(TestContext tc) => await Task.FromResult(true).ConfigureAwait(false);

        public static Task PublicStaticNonAsyncTaskMethodWithTC(TestContext tc) => Task.FromResult(true);

        [SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "Done on purpose")]
        public static async void PublicStaticAsyncVoidMethodWithTC(TestContext tc) => await Task.FromResult(true).ConfigureAwait(false);

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

        [Timeout(-11)]
        public void PublicMethodWithInvalidTimeout()
        {
        }

        [Timeout(11)]
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
