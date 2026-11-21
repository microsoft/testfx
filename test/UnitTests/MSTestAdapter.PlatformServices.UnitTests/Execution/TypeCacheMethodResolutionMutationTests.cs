// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if DEBUG && !NETFRAMEWORK
using AwesomeAssertions;

using Microsoft.Testing.TestInfrastructure;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

using TestFramework.ForTestingMSTest;
#endif

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

#if DEBUG && !NETFRAMEWORK
public class TypeCacheMethodResolutionMutationTests : TestContainer
{
    private const string TestMethodName = "TestMethod";

    private static readonly MethodInfo ResolveTestMethodInfoMethod = typeof(TypeCache).GetMethod("ResolveTestMethodInfo", BindingFlags.NonPublic | BindingFlags.Static)!;

    public void ResolveTestMethodInfoShouldAssertThatTheTestMethodIsNotNull()
    {
        TestClassInfo classInfo = CreateClassInfo();
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);

        Action act = () => ResolveTestMethodInfoMethod.Invoke(null, [null, classInfo]);

        act.Should().Throw<TargetInvocationException>().WithInnerException<NullReferenceException>();
        assertions.Should().Equal("testMethod is Null");
    }

    public void ResolveTestMethodInfoShouldAssertThatTheTestClassInfoIsNotNull()
    {
        TestMethod testMethod = CreateTestMethod();
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);

        Action act = () => ResolveTestMethodInfoMethod.Invoke(null, [testMethod, null]);

        act.Should().Throw<TargetInvocationException>().WithInnerException<NullReferenceException>();
        assertions.Should().Equal("testClassInfo is Null");
    }

    private static TestMethod CreateTestMethod()
        => new(TestMethodName, null, TestMethodName, typeof(ResolvableTestClass).FullName!, "A", null, null)
        {
            MethodInfo = typeof(ResolvableTestClass).GetMethod(TestMethodName),
        };

    private static TestClassInfo CreateClassInfo()
        => new(
            typeof(ResolvableTestClass),
            typeof(ResolvableTestClass).GetConstructor(Type.EmptyTypes),
            isParameterlessConstructor: true,
            new TestClassAttribute(),
            new TestAssemblyInfo(typeof(ResolvableTestClass).Assembly));

    [TestClass]
    public class ResolvableTestClass
    {
        public void TestMethod()
        {
        }
    }
}
#endif
