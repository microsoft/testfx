// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using System.Reflection.Emit;

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Helpers;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Helpers;

public sealed class ManagedNameHelperTests : TestContainer
{
    public void GetManagedNameAndHierarchy_ReportsConcreteTypeMethodAndHierarchy()
    {
        MethodInfo method = typeof(PlainFixture).GetMethod(nameof(PlainFixture.Accept))!;
        ManagedNameHelper.GetManagedNameAndHierarchy(method, out string type, out string name, out string?[] hierarchy);

        type.Should().Be("MSTestAdapter.PlatformServices.UnitTests.Helpers.ManagedNameHelperTests+PlainFixture");
        name.Should().Be("Accept(System.Int32,System.String[,])");
        hierarchy.Should().Equal(null, "MSTestAdapter.PlatformServices.UnitTests.Helpers", "ManagedNameHelperTests+PlainFixture", "Accept(System.Int32,System.String[,])");
    }

    public void GetManagedNameAndHierarchy_OpensClosedGenericTypesAndMethods()
    {
        MethodInfo method = typeof(GenericFixture<int>).GetMethod(nameof(GenericFixture<>.Mix))!.MakeGenericMethod(typeof(string));
        ManagedNameHelper.GetManagedNameAndHierarchy(method, out string type, out string name, out string?[] hierarchy);

        type.Should().Be("MSTestAdapter.PlatformServices.UnitTests.Helpers.ManagedNameHelperTests+GenericFixture`1");
        name.Should().Be("Mix`1(!0[,],System.Collections.Generic.List`1<!!0>,System.Collections.Generic.Dictionary`2<!0,!!0[]>)");
        hierarchy[2].Should().Be("ManagedNameHelperTests+GenericFixture`1");
        hierarchy[3].Should().Be(name);
    }

    public void GetManagedNameAndHierarchy_FormatsNestedClosedGenericAndJaggedArrayParameters()
    {
        MethodInfo method = typeof(PlainFixture).GetMethod(nameof(PlainFixture.Nested))!;
        ManagedNameHelper.GetManagedNameAndHierarchy(method, out _, out string name, out _);

        name.Should().Be("Nested(MSTestAdapter.PlatformServices.UnitTests.Helpers.ManagedNameHelperTests+GenericFixture`1+NestedFixture`1<System.Int32,System.String>,System.Int32[][])");
    }

    public void GetMethod_SelectsExactOverloadParameterCountAndGenericArity()
    {
        Assembly assembly = typeof(PlainFixture).Assembly;
        string type = typeof(PlainFixture).FullName!;
        MethodInfo integer = ManagedNameHelper.GetMethod(assembly, type, "Overload(System.Int32)");
        MethodInfo text = ManagedNameHelper.GetMethod(assembly, type, "Overload(System.String)");
        MethodInfo generic = ManagedNameHelper.GetMethod(assembly, type, "Echo`1(!!0)");
        MethodInfo empty = ManagedNameHelper.GetMethod(assembly, type, "Empty");

        integer.GetParameters()[0].ParameterType.Should().Be(typeof(int));
        text.GetParameters()[0].ParameterType.Should().Be(typeof(string));
        generic.IsGenericMethodDefinition.Should().BeTrue();
        generic.GetGenericArguments().Length.Should().Be(1);
        empty.Name.Should().Be("Empty");
        empty.GetParameters().Should().BeEmpty();
    }

    public void GetMethod_FindsPrivateStaticInheritedAndMostDerivedMethods()
    {
        Assembly assembly = typeof(DerivedFixture).Assembly;
        string type = typeof(DerivedFixture).FullName!;
        ManagedNameHelper.GetMethod(assembly, type, "PrivateStatic(System.String)").IsPrivate.Should().BeTrue();
        ManagedNameHelper.GetMethod(assembly, type, "PrivateStatic(System.String)").IsStatic.Should().BeTrue();
        ManagedNameHelper.GetMethod(assembly, type, "Inherited(System.Int32)").DeclaringType.Should().Be(typeof(BaseFixture));
        ManagedNameHelper.GetMethod(assembly, type, "Hidden(System.Int32)").DeclaringType.Should().Be(typeof(DerivedFixture));
    }

    public void GetMethod_RejectsAbsentTypeMethodWrongParametersAndWrongArity()
    {
        Assembly assembly = typeof(PlainFixture).Assembly;
        string type = typeof(PlainFixture).FullName!;
        foreach (string method in new[] { "Missing", "Overload", "Overload()", "Overload(System.Boolean)", "Overload(System.Int32,System.String)", "Echo`2(!!0)", string.Empty, "' '" })
        {
            Action action = () => ManagedNameHelper.GetMethod(assembly, type, method);
            action.Should().Throw<InvalidManagedNameException>($"'{method}' does not identify a fixture method");
        }

        Action absentType = () => ManagedNameHelper.GetMethod(assembly, "Missing.Type", "Empty");
        absentType.Should().Throw<InvalidManagedNameException>();
    }

    public void GetManagedNameAndHierarchy_GlobalNamespaceHasNullNamespaceAndContainer()
    {
        Type type = EmitType("GlobalFixture", "Ordinary");
        ManagedNameHelper.GetManagedNameAndHierarchy(type.GetMethod("Ordinary")!, out string managedType, out string method, out string?[] hierarchy);
        managedType.Should().Be("GlobalFixture");
        method.Should().Be("Ordinary");
        hierarchy.Should().Equal(null, null, "GlobalFixture", "Ordinary");
    }

    public void GetManagedNameAndHierarchy_EscapesMetadataNamesButPreservesValidUnicodeAndBalancedGenerics()
    {
        foreach ((string input, string expected) in new (string, string)[]
        {
            ("1start", "'1start'"),
            ("name with space", "'name with space'"),
            ("quote'name\\tail", "'quote\\'name\\\\tail'"),
            ("Balanced<Item>", "Balanced<Item>"),
            ("Empty<>", "'Empty<>'"),
            ("Unclosed<Item", "'Unclosed<Item'"),
            ("Extra>", "'Extra>'"),
            ("a.b", "a.b"),
            ("α_Ⅳ́", "α_Ⅳ́"),
            ("Version`nonnumeric", "'Version`nonnumeric'"),
            ("Version`0", "Version"),
        })
        {
            Type type = EmitType("1namespace.quoted space.Type Name", input);
            ManagedNameHelper.GetManagedNameAndHierarchy(type.GetMethod(input)!, out _, out string method, out _);
            method.Should().Be(expected, $"metadata name '{input}' has its own normalization partition");
        }
    }

    public void GetManagedNameAndHierarchy_RejectsNullAndConstructorMetadata()
    {
        Action missing = () => ManagedNameHelper.GetManagedNameAndHierarchy(null!, out _, out _, out _);
        missing.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("method");
        Action constructor = () => ManagedNameHelper.GetManagedNameAndHierarchy(typeof(PlainFixture).GetConstructor(Type.EmptyTypes)!, out _, out _, out _);
#if NETFRAMEWORK
        // Both assemblies compile a same-named internal polyfill. Pin the production
        // identity, rather than accidentally requiring the test assembly's copy.
        Exception error = constructor.Should().Throw<Exception>().Which;
        error.GetType().FullName.Should().Be("System.Diagnostics.UnreachableException");
        object assembly = error.GetType().Assembly;
        assembly.Should().BeSameAs(typeof(ManagedNameHelper).Assembly);
#else
        constructor.Should().Throw<System.Diagnostics.UnreachableException>();
#endif
    }

    public void GetManagedNameAndHierarchy_FormatsPointerRanksAndEscapedParameterNamespaces()
    {
        Type parameter = EmitType("1namespace.quoted space.Type Name", "Unused");
        Type globalParameter = EmitType("GlobalParameter", "Unused");
        Type type = EmitType("SignatureFixture", "Pointer", typeof(int).MakePointerType().MakePointerType(), parameter, globalParameter);

        ManagedNameHelper.GetManagedNameAndHierarchy(type.GetMethod("Pointer")!, out _, out string method, out _);

        method.Should().Be("Pointer(System.Int32**,'1namespace'.'quoted space'.'Type Name',GlobalParameter)");
    }

    private static Type EmitType(string typeName, string methodName, params Type[] parameterTypes)
    {
        var name = new AssemblyName("ManagedNameFixture" + Guid.NewGuid().ToString("N"));
        var assembly = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);
        ModuleBuilder module = assembly.DefineDynamicModule(name.Name!);
        TypeBuilder type = module.DefineType(typeName, TypeAttributes.Public);
        MethodBuilder method = type.DefineMethod(methodName, MethodAttributes.Public | MethodAttributes.Static, typeof(void), parameterTypes);
        method.GetILGenerator().Emit(OpCodes.Ret);
        return type.CreateTypeInfo()!.AsType();
    }

    public sealed class PlainFixture
    {
        public void Accept(int number, string[,] names)
        {
        }

        public void Nested(GenericFixture<int>.NestedFixture<string> item, int[][] values)
        {
        }

        public void Overload(int value)
        {
        }

        public void Overload(string value)
        {
        }

        public void Echo<T>(T value)
        {
        }

        public void Empty()
        {
        }
    }

    public sealed class GenericFixture<T>
        where T : notnull
    {
        public void Mix<TItem>(T[,] values, List<TItem> items, Dictionary<T, TItem[]> map)
        {
        }

        public sealed class NestedFixture<TItem>;
    }

    public class BaseFixture
    {
        public void Inherited(int value)
        {
        }

        public void Hidden(int value)
        {
        }
    }

    public sealed class DerivedFixture : BaseFixture
    {
        public new void Hidden(int value)
        {
        }

        [SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "This fixture is resolved by ManagedNameHelper.GetMethod to verify private static lookup.")]
        private static void PrivateStatic(string value)
        {
        }
    }
}
