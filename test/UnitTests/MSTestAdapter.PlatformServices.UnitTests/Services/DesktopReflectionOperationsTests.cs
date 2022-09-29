﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET462
namespace MSTestAdapter.PlatformServices.UnitTests.Services;

using System;
using System.Linq;
using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using MSTestAdapter.PlatformServices.UnitTests.Utilities;

using TestFramework.ForTestingMSTest;

public class DesktopReflectionOperationsTests : TestContainer
{
    private readonly ReflectionOperations _reflectionOperations;

    public DesktopReflectionOperationsTests()
    {
        _reflectionOperations = new ReflectionOperations();
    }

    public void GetCustomAttributesShouldReturnAllAttributes()
    {
        var methodInfo = typeof(ReflectionUtilityTests.DummyBaseTestClass).GetMethod("DummyVTestMethod1");

        var attributes = _reflectionOperations.GetCustomAttributes(methodInfo, false);

        Verify(attributes is not null);
        Verify(2 == attributes.Length);

        var expectedAttributes = new string[] { "DummyA : base", "DummySingleA : base" };
        Verify(expectedAttributes.SequenceEqual(ReflectionUtilityTests.GetAttributeValuePairs(attributes)));
    }

    public void GetCustomAttributesOnTypeShouldReturnAllAttributes()
    {
        var typeInfo = typeof(ReflectionUtilityTests.DummyBaseTestClass).GetTypeInfo();

        var attributes = _reflectionOperations.GetCustomAttributes(typeInfo, false);

        Verify(attributes is not null);
        Verify(1 == attributes.Length);

        var expectedAttributes = new string[] { "DummyA : ba" };
        Verify(expectedAttributes.SequenceEqual(ReflectionUtilityTests.GetAttributeValuePairs(attributes)));
    }

    public void GetSpecificCustomAttributesOnAssemblyShouldReturnAllAttributes()
    {
        var asm = typeof(ReflectionUtilityTests.DummyTestClass).Assembly;

        var attributes = _reflectionOperations.GetCustomAttributes(asm, typeof(ReflectionUtilityTests.DummyAAttribute));

        Verify(attributes is not null);
        Verify(2 == attributes.Length);

        var expectedAttributes = new string[] { "DummyA : a1", "DummyA : a2" };
        Verify(expectedAttributes.SequenceEqual(ReflectionUtilityTests.GetAttributeValuePairs(attributes)));
    }
}
#endif
