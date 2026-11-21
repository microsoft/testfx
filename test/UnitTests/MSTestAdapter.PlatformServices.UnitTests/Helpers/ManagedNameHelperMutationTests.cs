// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using System.Reflection.Emit;

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Helpers;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Helpers;

public sealed class ManagedNameHelperMutationTests : TestContainer
{
    public void GetMethod_ResolvesTheManagedTypeNameCaseSensitively()
    {
        Assembly assembly = typeof(CaseFixture).Assembly;
        string typeName = typeof(CaseFixture).FullName!;

        ManagedNameHelper.GetMethod(assembly, typeName, "Target").Name.Should().Be("Target", because: "the exact type name resolves");

        foreach (string wrongCase in new[] { typeName.ToUpperInvariant(), typeName.ToLowerInvariant() })
        {
            Action action = () => ManagedNameHelper.GetMethod(assembly, wrongCase, "Target");
            action.Should().Throw<InvalidManagedNameException>(because: $"'{wrongCase}' only differs from the real type name by its casing");
        }
    }

    public void GetManagedNameAndHierarchy_QuotesOnlyTheNamespacePartsThatNeedQuoting()
    {
        foreach ((string parameterType, string expected) in new (string, string)[]
        {
            ("1first.Plain.Param", "'1first'.Plain.Param"),
            ("Plain.1second.Param", "Plain.'1second'.Param"),
            ("A.1b.C.2d.E.Param", "A.'1b'.C.'2d'.E.Param"),
            ("A.b c.D.Param", "A.'b c'.D.Param"),
            ("Plain.Namespace.Param", "Plain.Namespace.Param"),
        })
        {
            MethodInfo method = EmitMethodTakingParameterOfType(parameterType);

            ManagedNameHelper.GetManagedNameAndHierarchy(method, out _, out string managedMethod, out _);

            managedMethod.Should().Be($"Method({expected})", because: $"only the parts of '{parameterType}' that start with a digit or contain invalid characters are quoted");
        }
    }

    public void GetManagedNameAndHierarchy_QuotesMethodNamesStartingWithABacktick()
    {
        foreach ((string name, bool generic, string expected) in new (string, bool, string)[]
        {
            ("`0", false, "'`0'"),
            ("`1", true, "'`1'`1"),
        })
        {
            MethodInfo method = EmitMethod(name, generic);

            ManagedNameHelper.GetManagedNameAndHierarchy(method, out _, out string managedMethod, out _);

            managedMethod.Should().Be(expected, because: $"'{name}' has no name before its backtick, so it is not an arity suffix");
        }
    }

    public void GetManagedNameAndHierarchy_QuotesMethodNamesWhoseGenericBracketsAreNotNestedProperly()
    {
        foreach ((string name, string expected) in new (string, string)[]
        {
            ("a>b<c", "'a>b<c'"),
            ("a<b>c", "a<b>c"),
        })
        {
            MethodInfo method = EmitMethod(name, generic: false);

            ManagedNameHelper.GetManagedNameAndHierarchy(method, out _, out string managedMethod, out _);

            managedMethod.Should().Be(expected, because: $"'{name}' is only valid as is when its closing bracket follows its opening bracket");
        }
    }

    public void GetManagedNameAndHierarchy_ClosesTheGenericArgumentsOfGenericArguments()
    {
        foreach ((string methodName, string expected) in new (string, string)[]
        {
            (nameof(ClosedGenericFixture.Map), "Map(System.Collections.Generic.Dictionary`2<System.Int32,System.Collections.Generic.List`1<System.String>>)"),
            (nameof(ClosedGenericFixture.Deep), "Deep(System.Collections.Generic.List`1<System.Collections.Generic.List`1<System.Collections.Generic.List`1<System.Int32>>>)"),
        })
        {
            MethodInfo method = typeof(ClosedGenericFixture).GetMethod(methodName)!;

            ManagedNameHelper.GetManagedNameAndHierarchy(method, out _, out string managedMethod, out _);

            managedMethod.Should().Be(expected, because: $"every nested generic argument of '{methodName}' lists its own arguments");
        }
    }

    public void GetManagedNameAndHierarchy_LeavesParametersWithoutTypeEmpty()
    {
        var method = new FakeMethod(typeof(ManagedNameHelperMutationTests), new FakeParameter(typeof(int)), new FakeParameter(null), new FakeParameter(typeof(string)));

        ManagedNameHelper.GetManagedNameAndHierarchy(method, out string managedType, out string managedMethod, out _);

        managedType.Should().Be(typeof(ManagedNameHelperMutationTests).FullName, because: "the reflected type provides the managed type name");
        managedMethod.Should().Be("Method(System.Int32,,System.String)", because: "a parameter without a type contributes nothing between its separators");
    }

    public void GetManagedNameAndHierarchy_ToleratesNestedTypesThatLoseTheirDeclaringType()
    {
        var method = new FakeMethod(typeof(ManagedNameHelperMutationTests), new FakeParameter(new NestedTypeLosingItsDeclaringType(typeof(string))));

        ManagedNameHelper.GetManagedNameAndHierarchy(method, out _, out string managedMethod, out _);

        managedMethod.Should().Be("Method(System.+String)", because: "a missing declaring type ends the nesting chain instead of failing");
    }

    public void GetManagedNameAndHierarchy_GivesNoArityToATypeThatIsNotGeneric()
    {
        var method = new FakeMethod(typeof(ManagedNameHelperMutationTests), new FakeParameter(new NonGenericTypeNamedLikeAGenericOne(typeof(string))));

        ManagedNameHelper.GetManagedNameAndHierarchy(method, out _, out string managedMethod, out _);

        managedMethod.Should().Be("Method(System.'List`1')", because: "the backtick of a type that is not generic belongs to its name instead of being an arity suffix");
    }

    private static ModuleBuilder DefineModule()
    {
        var name = new AssemblyName("ManagedNameMutationFixture" + Guid.NewGuid().ToString("N"));
        var assembly = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);
        return assembly.DefineDynamicModule(name.Name!);
    }

    private static MethodInfo EmitMethod(string methodName, bool generic)
    {
        TypeBuilder type = DefineModule().DefineType("MutationFixture", TypeAttributes.Public);
        MethodBuilder method = type.DefineMethod(methodName, MethodAttributes.Public | MethodAttributes.Static, typeof(void), Type.EmptyTypes);
        if (generic)
        {
            method.DefineGenericParameters("T");
        }

        method.GetILGenerator().Emit(OpCodes.Ret);
        return type.CreateTypeInfo()!.AsType().GetMethod(methodName)!;
    }

    private static MethodInfo EmitMethodTakingParameterOfType(string parameterTypeFullName)
    {
        ModuleBuilder module = DefineModule();
        Type parameterType = module.DefineType(parameterTypeFullName, TypeAttributes.Public).CreateTypeInfo()!.AsType();
        TypeBuilder holder = module.DefineType("Holder", TypeAttributes.Public);
        MethodBuilder method = holder.DefineMethod("Method", MethodAttributes.Public | MethodAttributes.Static, typeof(void), [parameterType]);
        method.GetILGenerator().Emit(OpCodes.Ret);
        return holder.CreateTypeInfo()!.AsType().GetMethod("Method")!;
    }

    public sealed class CaseFixture
    {
        public void Target()
        {
        }
    }

    public sealed class ClosedGenericFixture
    {
        public void Map(Dictionary<int, List<string>> map)
        {
        }

        public void Deep(List<List<List<int>>> values)
        {
        }
    }

    private sealed class FakeParameter(Type? parameterType) : ParameterInfo
    {
        public override Type ParameterType => parameterType!;
    }

    private sealed class NestedTypeLosingItsDeclaringType(Type type) : TypeDelegator(type)
    {
        private int _declaringTypeReads;

        public override Type? DeclaringType => _declaringTypeReads++ == 0 ? typeof(object) : null;

        public override Type[] GetGenericArguments() => Type.EmptyTypes;
    }

    private sealed class NonGenericTypeNamedLikeAGenericOne(Type type) : TypeDelegator(type)
    {
        public override string Name => "List`1";

        public override Type[] GenericTypeArguments => [typeof(int)];

        public override Type[] GetGenericArguments() => Type.EmptyTypes;
    }

    private sealed class FakeMethod(Type reflectedType, params ParameterInfo[] parameters) : MethodInfo
    {
        public override string Name => "Method";

        public override Type? DeclaringType => reflectedType;

        public override Type? ReflectedType => reflectedType;

        public override ICustomAttributeProvider ReturnTypeCustomAttributes => throw new NotSupportedException();

        public override RuntimeMethodHandle MethodHandle => throw new NotSupportedException();

        public override MethodAttributes Attributes => MethodAttributes.Public | MethodAttributes.Static;

        public override Type[] GetGenericArguments() => Type.EmptyTypes;

        public override ParameterInfo[] GetParameters() => parameters;

        public override MethodInfo GetBaseDefinition() => this;

        public override MethodImplAttributes GetMethodImplementationFlags() => MethodImplAttributes.IL;

        public override object[] GetCustomAttributes(bool inherit) => [];

        public override object[] GetCustomAttributes(Type attributeType, bool inherit) => [];

        public override bool IsDefined(Type attributeType, bool inherit) => false;

        public override object? Invoke(object? obj, BindingFlags invokeAttr, Binder? binder, object?[]? parameters, CultureInfo? culture)
            => throw new NotSupportedException();
    }
}
