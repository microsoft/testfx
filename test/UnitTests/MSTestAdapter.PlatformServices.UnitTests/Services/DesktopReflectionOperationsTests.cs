// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET462
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using MSTestAdapter.PlatformServices.UnitTests.Utilities;

using TestFramework.ForTestingMSTest;

using FluentAssertions;

namespace MSTestAdapter.PlatformServices.UnitTests.Services;

public class DesktopReflectionOperationsTests : TestContainer
{
    private readonly ReflectionOperations _reflectionOperations;

    public DesktopReflectionOperationsTests() => _reflectionOperations = new ReflectionOperations();

    public void GetCustomAttributesShouldReturnAllAttributes()
    {
        MethodInfo methodInfo = typeof(ReflectionUtilityTests.DummyBaseTestClass).GetMethod("DummyVTestMethod1");

        object[] attributes = _reflectionOperations.GetCustomAttributes(methodInfo, false);

        attributes.Should().NotBeNull();
        attributes.Length.Should().Be(2);

        string[] expectedAttributes = ["DummyA : base", "DummySingleA : base"];
        Verify(expectedAttributes.SequenceEqual(ReflectionUtilityTests.GetAttributeValuePairs(attributes)));
    }

    public void GetCustomAttributesOnTypeShouldReturnAllAttributes()
    {
        Type type = typeof(ReflectionUtilityTests.DummyBaseTestClass);

        object[] attributes = _reflectionOperations.GetCustomAttributes(type, false);

        attributes.Should().NotBeNull();
        attributes.Length.Should().Be(1);

        string[] expectedAttributes = ["DummyA : ba"];
        Verify(expectedAttributes.SequenceEqual(ReflectionUtilityTests.GetAttributeValuePairs(attributes)));
    }

    public void GetSpecificCustomAttributesOnAssemblyShouldReturnAllAttributes()
    {
        Assembly asm = typeof(ReflectionUtilityTests.DummyTestClass).Assembly;

        object[] attributes = _reflectionOperations.GetCustomAttributes(asm, typeof(ReflectionUtilityTests.DummyAAttribute));

        attributes.Should().NotBeNull();
        attributes.Length.Should().Be(2);

        string[] expectedAttributes = ["DummyA : a1", "DummyA : a2"];
        Verify(expectedAttributes.SequenceEqual(ReflectionUtilityTests.GetAttributeValuePairs(attributes)));
    }
}
#endif
