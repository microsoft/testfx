// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if DEBUG && !NETFRAMEWORK
using AwesomeAssertions;

using Microsoft.Testing.TestInfrastructure;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Discovery;

public partial class TypeEnumeratorTests
{
    // A generic type parameter is the only kind of type without an assembly-qualified name, and the discovery assertion documents that
    // such a type must never reach test creation. The rest of the method must keep working, so the type only hides its qualified name.
    public void GetTestFromMethodAssertsThatTheTypeHasAnAssemblyQualifiedName()
    {
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(new TypeWithoutAssemblyQualifiedName(typeof(DummyTestClass)), "DummyAssemblyName");
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);

        MSTest.TestAdapter.ObjectModel.UnitTestElement testElement = typeEnumerator.GetTestFromMethod(typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType")!, classDisablesParallelization: false, _warnings);

        assertions.Should().Equal("AssemblyQualifiedName for method is null.");
        testElement.TestMethod.Name.Should().Be("MethodWithVoidReturnType");
        testElement.TestMethod.FullClassName.Should().Be(typeof(DummyTestClass).FullName);
    }

    private sealed class TypeWithoutAssemblyQualifiedName : TypeDelegator
    {
        public TypeWithoutAssemblyQualifiedName(Type delegatingType)
            : base(delegatingType)
        {
        }

        public override string? AssemblyQualifiedName => null;
    }
}
#endif
