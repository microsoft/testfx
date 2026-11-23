// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Discovery;

public partial class TypeEnumeratorTests
{
    // Overloads share a name, so duplicate detection must key on the full method signature to keep every generated descriptor.
    public void EnumerateShouldKeepOverloadedGeneratedDescriptorsAsDistinctTests()
    {
        Type type = typeof(DescriptorOverloadedTestClass);
        MethodInfo[] methods =
        [
            type.GetMethod(nameof(DescriptorOverloadedTestClass.Overloaded), Type.EmptyTypes)!,
            type.GetMethod(nameof(DescriptorOverloadedTestClass.Overloaded), [typeof(int)])!,
        ];
        SetGeneratedDescriptorOperations(type, methods, areAllTestMethodsSupported: true);
        SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: false);
        TypeEnumerator typeEnumerator = GetTypeEnumeratorInstance(type, Assembly.GetExecutingAssembly().Location);

        List<MSTest.TestAdapter.ObjectModel.UnitTestElement>? tests = typeEnumerator.Enumerate(_warnings, useGeneratedDescriptors: true);

        tests.Should().HaveCount(2);
        tests.Should().OnlyContain(test => test.IsFromGeneratedDescriptor);
        tests.Select(test => test.TestMethod.MethodInfo).Should().Equal(methods);
        tests.Select(test => test.TestMethod.ParameterTypes).Should().Equal(string.Empty, "System.Int32");
    }
}

[TestClass]
public class DescriptorOverloadedTestClass
{
    [TestMethod]
    public void Overloaded()
    {
    }

    [TestMethod]
    public void Overloaded(int value)
    {
    }
}
