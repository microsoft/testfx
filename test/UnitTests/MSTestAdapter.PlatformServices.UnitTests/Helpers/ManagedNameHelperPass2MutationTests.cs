// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Helpers;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Helpers;

public sealed class ManagedNameHelperPass2MutationTests : TestContainer
{
    public void GetMethod_MatchesTheClosedGenericArgumentsOfAParameterType()
    {
        Assembly assembly = typeof(GenericParameterFixture).Assembly;
        string type = typeof(GenericParameterFixture).FullName!;

        MethodInfo integers = ManagedNameHelper.GetMethod(assembly, type, "Take(System.Collections.Generic.List`1<System.Int32>)");
        MethodInfo strings = ManagedNameHelper.GetMethod(assembly, type, "Take(System.Collections.Generic.List`1<System.String>)");

        integers.GetParameters()[0].ParameterType.Should().Be(typeof(List<int>), because: "the generic arguments are part of the parameter type name an overload is matched by");
        strings.GetParameters()[0].ParameterType.Should().Be(typeof(List<string>), because: "the generic arguments are part of the parameter type name an overload is matched by");
    }

    public void GetMethod_ResolvesTheManagedNameGeneratedForNestedClosedGenericParameters()
    {
        MethodInfo method = typeof(GenericParameterFixture).GetMethod(nameof(GenericParameterFixture.Map))!;

        ManagedNameHelper.GetManagedNameAndHierarchy(method, out string managedType, out string managedMethod, out _);
        MethodInfo resolved = ManagedNameHelper.GetMethod(typeof(GenericParameterFixture).Assembly, managedType, managedMethod);

        managedMethod.Should().Be("Map(System.Collections.Generic.Dictionary`2<System.Int32,System.Collections.Generic.List`1<System.String>>)");
        resolved.Should().BeSameAs(method, because: "a generated managed name has to resolve back to the method it was generated from");
    }

    public void GetMethod_ReportsAnInheritedMethodHiddenByItsBaseTypeAsNotFoundWhenNoCandidateIsDeclaredOnTheRequestedType()
    {
        Type type = typeof(HiddenTwiceLeaf);
        type.GetMethods(BindingFlags.Public | BindingFlags.Instance).Count(method => method.Name == nameof(HiddenTwiceBase.Hidden))
            .Should().Be(2, because: "reflection reports both the hiding and the hidden method, and neither is declared on the leaf type");

        Action action = () => ManagedNameHelper.GetMethod(type.Assembly, type.FullName!, "Hidden(System.Int32)");

        action.Should().ThrowExactly<InvalidManagedNameException>(because: "no candidate is declared on the requested type, which has to be reported as an unknown managed name and not as an invalid operation");
    }

    public void GetManagedNameAndHierarchy_AppendsNoNamespaceWhenItIsNullByTheTimeItIsAppended()
    {
        // The namespace of a type is read twice: once to decide whether to append it, and once more to append it.
        var parameterType = new TypeLosingItsNamespace(typeof(string));
        var method = new FakeMethod(typeof(ManagedNameHelperPass2MutationTests), new FakeParameter(parameterType));

        ManagedNameHelper.GetManagedNameAndHierarchy(method, out string managedType, out string managedMethod, out _);

        managedType.Should().Be(typeof(ManagedNameHelperPass2MutationTests).FullName);
        managedMethod.Should().Be("Method(.String)", because: "a null namespace appends nothing, so only the separator that always follows the namespace is left in front of the type name");
    }

    public sealed class GenericParameterFixture
    {
        public void Take(List<int> values)
        {
        }

        public void Take(List<string> values)
        {
        }

        public void Map(Dictionary<int, List<string>> map)
        {
        }
    }

    public class HiddenTwiceBase
    {
        public void Hidden(int value)
        {
        }
    }

    public class HiddenTwiceMiddle : HiddenTwiceBase
    {
        public new void Hidden(int value)
        {
        }
    }

    public sealed class HiddenTwiceLeaf : HiddenTwiceMiddle;

    private sealed class TypeLosingItsNamespace(Type type) : TypeDelegator(type)
    {
        private int _namespaceReads;

        public override string? Namespace => _namespaceReads++ == 0 ? "System" : null;

        public override Type[] GetGenericArguments() => Type.EmptyTypes;
    }

    private sealed class FakeParameter(Type parameterType) : ParameterInfo
    {
        public override Type ParameterType => parameterType;
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
