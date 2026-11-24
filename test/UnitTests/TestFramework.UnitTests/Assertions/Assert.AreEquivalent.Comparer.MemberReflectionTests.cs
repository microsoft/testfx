// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using System.Reflection.Emit;

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public sealed class AreEquivalentComparerMemberReflectionTests : TestContainer
{
    public AreEquivalentComparerMemberReflectionTests()
        => EquivalenceComparerCaches.Reset();

    protected override void Dispose(bool disposing)
    {
        try
        {
            EquivalenceComparerCaches.Reset();
        }
        finally
        {
            base.Dispose(disposing);
        }
    }

    public void AreEquivalent_ExpectedMemberGetterFailureIdentifiesSideAndUnwrapsException()
    {
        ThrowingMember expected = new();
        SafeMember actual = new();
        Action compare = () => Assert.AreEquivalent<SafeMember>(expected, actual);

        AssertFailedException failure = compare.Should().ThrowExactly<AssertFailedException>().Which;
        failure.Message.Should().Contain("reading the expected member threw InvalidOperationException: expected getter failure");
        failure.Message.Should().NotContain("TargetInvocationException");
    }

    public void AreEquivalent_ActualMemberGetterFailureIdentifiesSideAndUnwrapsException()
    {
        SafeMember expected = new();
        ActualThrowingMember actual = new();
        Action compare = () => Assert.AreEquivalent<SafeMember>(expected, actual);

        AssertFailedException failure = compare.Should().ThrowExactly<AssertFailedException>().Which;
        failure.Message.Should().Contain("reading the actual member threw InvalidOperationException: actual getter failure");
        failure.Message.Should().NotContain("TargetInvocationException");
    }

    public void AreEquivalent_ActualMemberGetterAssertFailurePropagatesUnwrapped()
    {
        SafeMember expected = new();
        AssertFailingMember actual = new();
        Action compare = () => Assert.AreEquivalent<SafeMember>(expected, actual);

        AssertFailedException failure = compare.Should().ThrowExactly<AssertFailedException>()
            .WithMessage("*actual getter assertion*")
            .Which;
        failure.Message.Should().NotContain("Mismatch at");
    }

    public void AreEquivalent_PublicIndexersAndPropertiesWithoutPublicGettersAreIgnored()
    {
        FilteredMembers expected = new(indexerValue: 1, privateGetterValue: 2);
        FilteredMembers actual = new(indexerValue: 9, privateGetterValue: 8);

        Assert.AreEquivalent(expected, actual);
    }

    public void AreEquivalent_NewShadowedField_UsesMostDerivedDeclaration()
    {
        ShadowedFieldDerived expected = new() { Value = 200 };
        ShadowedFieldDerived actual = new() { Value = 999 };
        Action compare = () => Assert.AreEquivalent(expected, actual);

        compare.Should().ThrowExactly<AssertFailedException>()
            .WithMessage("*Value*200*999*");
    }

    public void AreEquivalent_NewShadowedProperty_OverridesBaseField()
    {
        ShadowedPropertyDerived expected = new() { Value = 200 };
        ShadowedPropertyDerived actual = new() { Value = 999 };
        Action compare = () => Assert.AreEquivalent(expected, actual);

        compare.Should().ThrowExactly<AssertFailedException>()
            .WithMessage("*Value*200*999*");
    }

    public void AreEquivalent_SameTypePropertyAndFieldName_PrefersProperty()
    {
        Type type = CreateTypeWithSameNamePropertyAndField();
        FieldInfo field = type.GetField("Value")!;
        object expected = Activator.CreateInstance(type)!;
        object actual = Activator.CreateInstance(type)!;
        field.SetValue(expected, 100);
        field.SetValue(actual, 999);

        Assert.AreEquivalent<object>(expected, actual);
    }

    private static Type CreateTypeWithSameNamePropertyAndField()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName($"MemberTest_{Guid.NewGuid():N}"), AssemblyBuilderAccess.Run);
        ModuleBuilder module = assembly.DefineDynamicModule("Members");
        TypeBuilder type = module.DefineType($"SameNameMembers_{Guid.NewGuid():N}", TypeAttributes.Public | TypeAttributes.Class);
        type.DefineField("Value", typeof(int), FieldAttributes.Public);

        PropertyBuilder property = type.DefineProperty("Value", PropertyAttributes.None, typeof(int), Type.EmptyTypes);
        MethodBuilder getter = type.DefineMethod(
            "get_Value",
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            typeof(int),
            Type.EmptyTypes);
        ILGenerator il = getter.GetILGenerator();
        il.Emit(OpCodes.Ldc_I4, 200);
        il.Emit(OpCodes.Ret);
        property.SetGetMethod(getter);

        return type.CreateType()!;
    }

    private class SafeMember
    {
        public virtual string Value => "same";
    }

    private sealed class ThrowingMember : SafeMember
    {
        public override string Value
            => throw new InvalidOperationException("expected getter failure");
    }

    private sealed class ActualThrowingMember : SafeMember
    {
        public override string Value
            => throw new InvalidOperationException("actual getter failure");
    }

    private sealed class AssertFailingMember : SafeMember
    {
        public override string Value
        {
            get
            {
                Assert.Fail("actual getter assertion");
                return string.Empty;
            }
        }
    }

    private sealed class FilteredMembers(int indexerValue, int privateGetterValue)
    {
        private readonly int _indexerValue = indexerValue;

        public int PublicValue => 1;

        public int this[int index]
            => _indexerValue + index;

        public int WriteOnly
        {
            set { }
        }

        public int PrivateGetter { private get; set; } = privateGetterValue;
    }

    private class ShadowedFieldBase
    {
#pragma warning disable SA1401 // Field should be public for the shadowed-member comparison case.
        public int Value = 100;
#pragma warning restore SA1401
    }

    private sealed class ShadowedFieldDerived : ShadowedFieldBase
    {
#pragma warning disable SA1401 // Field should be public for the shadowed-member comparison case.
        public new int Value = 200;
#pragma warning restore SA1401
    }

    private class ShadowedPropertyBase
    {
#pragma warning disable SA1401 // Field should be public for the shadowed-member comparison case.
        public int Value = 100;
#pragma warning restore SA1401
    }

    private sealed class ShadowedPropertyDerived : ShadowedPropertyBase
    {
        public new int Value { get; set; } = 200;
    }
}
