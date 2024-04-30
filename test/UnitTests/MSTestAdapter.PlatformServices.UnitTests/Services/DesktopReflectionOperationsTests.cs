﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET462

using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using MSTestAdapter.PlatformServices.UnitTests.Utilities;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Services;

public class DesktopReflectionOperationsTests : TestContainer
{
    private readonly ReflectionOperations _reflectionOperations;

    public DesktopReflectionOperationsTests()
    {
        _reflectionOperations = new ReflectionOperations();
    }

    public void GetCustomAttributesShouldReturnAllAttributes()
    {
        MethodInfo methodInfo = typeof(ReflectionUtilityTests.DummyBaseTestClass).GetMethod("DummyVTestMethod1");

        object[] attributes = _reflectionOperations.GetCustomAttributes(methodInfo, false);

        Verify(attributes is not null);
        Verify(attributes.Length == 2);

        string[] expectedAttributes = new string[] { "DummyA : base", "DummySingleA : base" };
        Verify(expectedAttributes.SequenceEqual(ReflectionUtilityTests.GetAttributeValuePairs(attributes)));
    }

    public void GetCustomAttributesOnTypeShouldReturnAllAttributes()
    {
        TypeInfo typeInfo = typeof(ReflectionUtilityTests.DummyBaseTestClass).GetTypeInfo();

        object[] attributes = _reflectionOperations.GetCustomAttributes(typeInfo, false);

        Verify(attributes is not null);
        Verify(attributes.Length == 1);

        string[] expectedAttributes = new string[] { "DummyA : ba" };
        Verify(expectedAttributes.SequenceEqual(ReflectionUtilityTests.GetAttributeValuePairs(attributes)));
    }

    public void GetSpecificCustomAttributesOnAssemblyShouldReturnAllAttributes()
    {
        Assembly asm = typeof(ReflectionUtilityTests.DummyTestClass).Assembly;

        object[] attributes = _reflectionOperations.GetCustomAttributes(asm, typeof(ReflectionUtilityTests.DummyAAttribute));

        Verify(attributes is not null);
        Verify(attributes.Length == 2);

        string[] expectedAttributes = new string[] { "DummyA : a1", "DummyA : a2" };
        Verify(expectedAttributes.SequenceEqual(ReflectionUtilityTests.GetAttributeValuePairs(attributes)));
    }
}
#endif
