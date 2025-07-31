// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using MSTest.PlatformServices.Utilities.UnitTests;

using TestFramework.ForTestingMSTest;

namespace MSTest.PlatformServices.Services.UnitTests;

public class DesktopReflectionOperationsTests : TestContainer
{
    private readonly ReflectionOperations _reflectionOperations;

    public DesktopReflectionOperationsTests() => _reflectionOperations = new ReflectionOperations();

    public void GetCustomAttributesShouldReturnAllAttributes()
    {
        MethodInfo methodInfo = typeof(ReflectionUtilityTests.DummyBaseTestClass).GetMethod("DummyVTestMethod1");

        object[] attributes = _reflectionOperations.GetCustomAttributes(methodInfo);

        Verify(attributes is not null);
        Verify(attributes.Length == 2);

        string[] expectedAttributes = ["DummyA : base", "DummySingleA : base"];
        Verify(expectedAttributes.SequenceEqual(ReflectionUtilityTests.GetAttributeValuePairs(attributes)));
    }

    public void GetCustomAttributesOnTypeShouldReturnAllAttributes()
    {
        Type type = typeof(ReflectionUtilityTests.DummyBaseTestClass);

        object[] attributes = _reflectionOperations.GetCustomAttributes(type);

        Verify(attributes is not null);
        Verify(attributes.Length == 1);

        string[] expectedAttributes = ["DummyA : ba"];
        Verify(expectedAttributes.SequenceEqual(ReflectionUtilityTests.GetAttributeValuePairs(attributes)));
    }

    public void GetSpecificCustomAttributesOnAssemblyShouldReturnAllAttributes()
    {
        Assembly asm = typeof(ReflectionUtilityTests.DummyTestClass).Assembly;

        object[] attributes = _reflectionOperations.GetCustomAttributes(asm, typeof(ReflectionUtilityTests.DummyAAttribute));

        Verify(attributes is not null);
        Verify(attributes.Length == 2);

        string[] expectedAttributes = ["DummyA : a1", "DummyA : a2"];
        Verify(expectedAttributes.SequenceEqual(ReflectionUtilityTests.GetAttributeValuePairs(attributes)));
    }
}
#endif
